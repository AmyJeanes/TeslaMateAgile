using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;
using TeslaMateAgile.Data;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Services.Interfaces;

namespace TeslaMateAgile.Services;

public class EnerginetService : IDynamicPriceDataService
{
    private readonly HttpClient _client;
    private readonly EnerginetOptions _options;
    private readonly FixedPriceService _fixedPriceService;

    public EnerginetService(HttpClient client, IOptions<EnerginetOptions> options)
    {
        _client = client;
        _options = options.Value;

        if (_options.FixedPrices != null)
        {
            _fixedPriceService = new FixedPriceService(Options.Create(_options.FixedPrices));
        }
    }

    public async Task<IEnumerable<Price>> GetPriceData(DateTimeOffset from, DateTimeOffset to)
    {
        var url = "DayAheadPrices?offset=0&start=" + from.AddHours(-2).AddMinutes(-1).UtcDateTime.ToString("yyyy-MM-ddTHH:mm") + "&end=" + to.AddHours(2).AddMinutes(1).UtcDateTime.ToString("yyyy-MM-ddTHH:mm") + "&filter={\"PriceArea\":[\"" + _options.Region + "\"]}&sort=TimeUTC ASC&timezone=dk".Replace(@"\", string.Empty); ;
        var resp = await _client.GetAsync(url);

        resp.EnsureSuccessStatusCode();

        var prices = new List<Price>();
        var EnerginetResponse = await JsonSerializer.DeserializeAsync<EnerginetResponse>(await resp.Content.ReadAsStreamAsync());

        if (EnerginetResponse.Records.Count > 0)
        {
            foreach (var record in EnerginetResponse.Records)
            {
                decimal fixedPrice = 0;
                if (_fixedPriceService != null)
                {
                    var fixedPrices = await _fixedPriceService.GetPriceData(record.TimeUTC, record.TimeUTC.AddHours(1));
                    fixedPrice = fixedPrices.Sum(p => p.Value);
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
                    ValidFrom = record.TimeUTC,
                    ValidTo = record.TimeUTC.AddMinutes(15),
                    Value = price
                });
            }
        }

        return prices;
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
