using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;
using TeslaMateAgile.Data;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Helpers.Interfaces;
using TeslaMateAgile.Services.Interfaces;

namespace TeslaMateAgile.Services;

public class EnerginetService : IDynamicPriceDataService
{
    private readonly HttpClient _client;
    private readonly IRateLimitHelper _rateLimitHelper;
    private readonly EnerginetOptions _options;
    private readonly FixedPriceService _fixedPriceService;

    /// <summary>
    /// Length of a single Energinet day ahead record. The dataset moved from hourly to
    /// quarter hourly resolution on 1 October 2025.
    /// </summary>
    private static readonly TimeSpan RecordDuration = TimeSpan.FromMinutes(15);

    public EnerginetService(HttpClient client, IRateLimitHelper rateLimitHelper, IOptions<EnerginetOptions> options)
    {
        _client = client;
        _rateLimitHelper = rateLimitHelper;
        _options = options.Value;

        if (_options.FixedPrices != null)
        {
            _fixedPriceService = new FixedPriceService(Options.Create(_options.FixedPrices));
        }
    }

    public async Task<PriceData> GetPriceData(DateTimeOffset from, DateTimeOffset to)
    {
        var url = "DayAheadPrices?offset=0&start=" + from.AddHours(-2).AddMinutes(-1).UtcDateTime.ToString("yyyy-MM-ddTHH:mm") + "&end=" + to.AddHours(2).AddMinutes(1).UtcDateTime.ToString("yyyy-MM-ddTHH:mm") + "&filter={\"PriceArea\":[\"" + _options.Region + "\"]}&sort=TimeUTC ASC&timezone=dk".Replace(@"\", string.Empty); ;
        _rateLimitHelper.AddRequest();
        var resp = await _client.GetAsync(url);

        resp.EnsureSuccessStatusCode();

        var prices = new List<Price>();
        var EnerginetResponse = await JsonSerializer.DeserializeAsync<EnerginetResponse>(await resp.Content.ReadAsStreamAsync());

        if (EnerginetResponse.Records.Count > 0)
        {
            foreach (var record in EnerginetResponse.Records)
            {
                var recordFrom = record.TimeUTC;
                var recordTo = record.TimeUTC.Add(RecordDuration);

                decimal fixedPrice = 0;
                if (_fixedPriceService != null)
                {
                    var fixedPriceData = await _fixedPriceService.GetPriceData(recordFrom, recordTo);
                    fixedPrice = WeightedFixedPrice(fixedPriceData.Prices, recordFrom, recordTo);
                }

                var spotPrice = _options.Currency switch
                {
                    EnerginetCurrency.DKK => record.DayAheadPriceDKK,
                    EnerginetCurrency.EUR => record.DayAheadPriceEUR,
                    _ => throw new ArgumentOutOfRangeException(nameof(_options.Currency)),
                };

                if (_options.ClampNegativePrices)
                {
                    spotPrice = Math.Max(0, spotPrice);
                }

                var price = (spotPrice / 1000) + fixedPrice;
                if (_options.VAT.HasValue)
                {
                    price *= _options.VAT.Value;
                }
                prices.Add(new Price
                {
                    ValidFrom = recordFrom,
                    ValidTo = recordTo,
                    Value = price
                });
            }
        }

        return new PriceData(prices);
    }

    /// <summary>
    /// Combines the fixed prices that overlap a single record, weighted by how much of the
    /// record each one covers. Summing them outright would count a fixed price in full even
    /// when it only applies to part of the record, which happens whenever a fixed price
    /// boundary falls inside it.
    /// </summary>
    private static decimal WeightedFixedPrice(IEnumerable<Price> fixedPrices, DateTimeOffset recordFrom, DateTimeOffset recordTo)
    {
        var recordTicks = (decimal)(recordTo - recordFrom).Ticks;
        if (recordTicks <= 0) { return 0; }

        var weighted = 0M;
        foreach (var fixedPrice in fixedPrices)
        {
            var overlapFrom = fixedPrice.ValidFrom > recordFrom ? fixedPrice.ValidFrom : recordFrom;
            var overlapTo = fixedPrice.ValidTo < recordTo ? fixedPrice.ValidTo : recordTo;
            var overlapTicks = (decimal)(overlapTo - overlapFrom).Ticks;
            if (overlapTicks <= 0) { continue; }

            weighted += fixedPrice.Value * (overlapTicks / recordTicks);
        }

        return weighted;
    }

    private class EnerginetResponse
    {
        [JsonPropertyName("records")]
        public List<EnerginetResponseRow> Records { get; set; }
    }

    private class EnerginetResponseRow
    {
        private DateTime _timeUTC;

        [JsonPropertyName("TimeUTC")]
        public DateTime TimeUTC { get => _timeUTC; set => _timeUTC = DateTime.SpecifyKind(value, DateTimeKind.Utc); }

        [JsonPropertyName("DayAheadPriceDKK")]
        public decimal DayAheadPriceDKK { get; set; }

        [JsonPropertyName("DayAheadPriceEUR")]
        public decimal DayAheadPriceEUR { get; set; }
    }
}
