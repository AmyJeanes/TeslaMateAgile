using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;
using TeslaMateAgile.Data;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Helpers.Interfaces;
using TeslaMateAgile.Services.Interfaces;

namespace TeslaMateAgile.Services;

public class OctopusService : IDynamicPriceDataService
{
    private readonly HttpClient _client;
    private readonly IRateLimitHelper _rateLimitHelper;
    private readonly OctopusOptions _options;

    public OctopusService(HttpClient client, IRateLimitHelper rateLimitHelper, IOptions<OctopusOptions> options)
    {
        _client = client;
        _rateLimitHelper = rateLimitHelper;
        _options = options.Value;
    }

    public async Task<PriceData> GetPriceData(DateTimeOffset from, DateTimeOffset to)
    {
        var url = $"products/{_options.ProductCode}/electricity-tariffs/{_options.TariffCode}-{_options.RegionCode}/standard-unit-rates?period_from={from.UtcDateTime:o}&period_to={to.UtcDateTime:o}";
        var list = new List<AgilePrice>();
        do
        {
            _rateLimitHelper.AddRequest();
            var resp = await _client.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            var agileResponse = await JsonSerializer.DeserializeAsync<AgileResponse>(await resp.Content.ReadAsStreamAsync()) ?? throw new Exception($"Deserialization of Octopus Agile API response failed");
            list.AddRange(agileResponse.Results);
            url = agileResponse.Next;
            if (string.IsNullOrEmpty(url))
            {
                break;
            }
            else
            {
                Thread.Sleep(5000); // back off API so they don't ban us
            }
        }
        while (true);
        return new PriceData(list
            .Select(x => new Price
            {
                Value = x.ValueIncVAT / 100,
                ValidFrom = x.ValidFrom,
                ValidTo = x.ValidTo
            }));
    }

    public class AgilePrice
    {
        [JsonPropertyName("value_exc_vat")]
        public decimal ValueExcVAT { get; set; }

        [JsonPropertyName("value_inc_vat")]
        public decimal ValueIncVAT { get; set; }

        [JsonPropertyName("valid_from")]
        public DateTimeOffset ValidFrom { get; set; }

        [JsonPropertyName("valid_to")]
        public DateTimeOffset ValidTo { get; set; }
    }

    public class AgileResponse
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("next")]
        public string Next { get; set; }

        [JsonPropertyName("previous")]
        public string Previous { get; set; }

        [JsonPropertyName("results")]
        public List<AgilePrice> Results { get; set; }
    }
}
