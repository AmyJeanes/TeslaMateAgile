using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using TeslaMateAgile.Data;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Services.Interfaces;

namespace TeslaMateAgile.Services;

public class PGEService : IDynamicPriceDataService
{
    private readonly HttpClient _client;
    private readonly PGEOptions _options;

    public PGEService(HttpClient client, IOptions<PGEOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<IEnumerable<Price>> GetPriceData(DateTimeOffset from, DateTimeOffset to)
    {
        // PGE publishes pricing at 6pm for the following day
        // We need to fetch data to cover the requested period
        var prices = new List<Price>();
        
        // Group requests by date range to minimize API calls
        var currentDate = from.Date;
        var endDate = to.Date;
        
        while (currentDate <= endDate)
        {
            var startDateStr = currentDate.ToString("yyyyMMdd");
            var endDateStr = currentDate.AddDays(1).ToString("yyyyMMdd");
            
            var queryParams = new Dictionary<string, string>
            {
                { "utility", _options.Utility },
                { "market", _options.Market },
                { "startdate", startDateStr },
                { "enddate", endDateStr },
                { "ratename", _options.RateName },
                { "representativeCircuitId", _options.RepresentativeCircuitId },
                { "program", _options.Program }
            };
            
            var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
            var url = $"/v1/getPricing?{queryString}";
            
            var resp = await _client.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var pgeResponse = await JsonSerializer.DeserializeAsync<PGEResponse>(await resp.Content.ReadAsStreamAsync(), jsonOptions);
            if (pgeResponse == null)
            {
                throw new Exception($"Deserialization of PGE API response failed");
            }
            
            if (pgeResponse.Data != null && pgeResponse.Data.Count > 0)
            {
                foreach (var priceCurve in pgeResponse.Data)
                {
                    if (priceCurve.PriceDetails != null)
                    {
                        prices.AddRange(priceCurve.PriceDetails.Select(x => new Price
                        {
                            Value = decimal.Parse(x.IntervalPrice),
                            ValidFrom = ParsePGEDateTime(x.StartIntervalTimeStamp),
                            ValidTo = ParsePGEDateTime(x.StartIntervalTimeStamp).AddMinutes(priceCurve.PriceHeader.IntervalLengthInMinutes)
                        }));
                    }
                }
            }
            
            currentDate = currentDate.AddDays(1);
            
            // Rate limit to avoid overwhelming the API
            if (currentDate <= endDate)
            {
                await Task.Delay(500);
            }
        }
        
        // Filter to return prices that overlap with the requested range
        // A price overlaps if it starts before the range ends AND ends after the range starts
        return prices.Where(p => p.ValidFrom < to && p.ValidTo > from);
    }

    private static DateTimeOffset ParsePGEDateTime(string dateTimeString)
    {
        // PGE returns dates like "2025-10-26T00:00:00-0700" 
        // which need special parsing because the timezone offset lacks a colon
        if (DateTimeOffset.TryParseExact(
            dateTimeString,
            new[] { "yyyy-MM-ddTHH:mm:sszzz", "yyyy-MM-ddTHH:mm:sszz" },
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var result))
        {
            return result;
        }
        
        // Fallback to standard parsing
        return DateTimeOffset.Parse(dateTimeString);
    }

    public class PriceComponent
    {
        [JsonPropertyName("component")]
        public string Component { get; set; }

        [JsonPropertyName("intervalPrice")]
        public string IntervalPrice { get; set; }

        [JsonPropertyName("priceType")]
        public string PriceType { get; set; }
    }

    public class PriceDetail
    {
        [JsonPropertyName("startIntervalTimeStamp")]
        public string StartIntervalTimeStamp { get; set; }

        [JsonPropertyName("intervalPrice")]
        public string IntervalPrice { get; set; }

        [JsonPropertyName("priceStatus")]
        public string PriceStatus { get; set; }

        [JsonPropertyName("priceComponents")]
        public List<PriceComponent> PriceComponents { get; set; }
    }

    public class PriceHeader
    {
        [JsonPropertyName("priceCurveName")]
        public string PriceCurveName { get; set; }

        [JsonPropertyName("marketName")]
        public string MarketName { get; set; }

        [JsonPropertyName("intervalLengthInMinutes")]
        public int IntervalLengthInMinutes { get; set; }

        [JsonPropertyName("settlementCurrency")]
        public string SettlementCurrency { get; set; }

        [JsonPropertyName("settlementUnit")]
        public string SettlementUnit { get; set; }

        [JsonPropertyName("startTime")]
        public string StartTime { get; set; }

        [JsonPropertyName("endTime")]
        public string EndTime { get; set; }

        [JsonPropertyName("recordCount")]
        public int RecordCount { get; set; }
    }

    public class PriceCurve
    {
        [JsonPropertyName("priceHeader")]
        public PriceHeader PriceHeader { get; set; }

        [JsonPropertyName("priceDetails")]
        public List<PriceDetail> PriceDetails { get; set; }
    }

    public class ResponseMeta
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("requestURL")]
        public string RequestURL { get; set; }

        [JsonPropertyName("requestBody")]
        public string RequestBody { get; set; }

        [JsonPropertyName("response")]
        public string Response { get; set; }
    }

    public class PGEResponse
    {
        [JsonPropertyName("meta")]
        public ResponseMeta Meta { get; set; }

        [JsonPropertyName("data")]
        public List<PriceCurve> Data { get; set; }
    }
}
