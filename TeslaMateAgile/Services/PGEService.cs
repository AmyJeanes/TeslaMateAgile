using Microsoft.Extensions.Logging;
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
    private readonly ILogger<PGEService> _logger;

    public PGEService(HttpClient client, IOptions<PGEOptions> options, ILogger<PGEService> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
        
        // Validate required options
        if (string.IsNullOrWhiteSpace(_options.RateName))
        {
            throw new InvalidOperationException("PGE RateName is required");
        }
        
        if (string.IsNullOrWhiteSpace(_options.RepresentativeCircuitId))
        {
            throw new InvalidOperationException("PGE RepresentativeCircuitId is required");
        }
        
        _logger.LogInformation("PGE Service initialized with RateName: {RateName}, Circuit: {CircuitId}", 
            _options.RateName, _options.RepresentativeCircuitId);
    }

    public async Task<IEnumerable<Price>> GetPriceData(DateTimeOffset from, DateTimeOffset to)
    {
        _logger.LogInformation("Fetching PGE price data from {From} UTC to {To} UTC", from.UtcDateTime, to.UtcDateTime);
        
        // PGE publishes pricing at 6pm for the following day
        // We need to fetch data to cover the requested period
        var prices = new List<Price>();
        
        // Expand the date range to ensure we get complete coverage
        // Convert to Pacific time to determine the correct local dates to request
        var pacificTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
        var fromPacific = TimeZoneInfo.ConvertTimeFromUtc(from.UtcDateTime, pacificTimeZone);
        var toPacific = TimeZoneInfo.ConvertTimeFromUtc(to.UtcDateTime, pacificTimeZone);
        
        _logger.LogDebug("UTC range: {FromUTC} to {ToUTC}", from.UtcDateTime, to.UtcDateTime);
        _logger.LogDebug("Pacific range: {FromPacific} to {ToPacific}", fromPacific, toPacific);
        
        // Request data for all days that might contain the needed intervals
        var currentDate = fromPacific.Date.AddDays(-1); // Start one day earlier to ensure coverage
        var endDate = toPacific.Date.AddDays(1); // End one day later to ensure coverage
        
        _logger.LogDebug("Will request PGE data for Pacific dates: {StartDate} to {EndDate}", 
            currentDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));
            
        while (currentDate <= endDate)
        {
            var startDateStr = currentDate.ToString("yyyyMMdd");
            var endDateStr = currentDate.AddDays(1).ToString("yyyyMMdd");
            
            _logger.LogDebug("Requesting PGE data for Pacific date: {CurrentDate} (startdate={StartDate}, enddate={EndDate})", 
                currentDate.ToString("yyyy-MM-dd"), startDateStr, endDateStr);
            
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
            
            _logger.LogDebug("Requesting PGE data for {CurrentDate} (startdate={StartDate}, enddate={EndDate}): {Url}", 
                currentDate.ToString("yyyy-MM-dd"), startDateStr, endDateStr, url);
            
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
                        _logger.LogDebug("Processing {Count} price intervals from {StartTime} to {EndTime} with {IntervalLength} minute intervals", 
                            priceCurve.PriceDetails.Count, 
                            priceCurve.PriceHeader.StartTime,
                            priceCurve.PriceHeader.EndTime,
                            priceCurve.PriceHeader.IntervalLengthInMinutes);
                        
                        // Validate we have complete daily coverage
                        var expectedIntervalsPerDay = 24 * 60 / priceCurve.PriceHeader.IntervalLengthInMinutes;
                        if (priceCurve.PriceDetails.Count < expectedIntervalsPerDay)
                        {
                            _logger.LogWarning("Incomplete daily data: expected {Expected} intervals but got {Actual} for date {CurrentDate}", 
                                expectedIntervalsPerDay, priceCurve.PriceDetails.Count, currentDate.ToString("yyyy-MM-dd"));
                        }
                            
                        var parsedPrices = priceCurve.PriceDetails.Select(x => {
                            var validFrom = ParsePGEDateTime(x.StartIntervalTimeStamp);
                            var validTo = validFrom.AddMinutes(priceCurve.PriceHeader.IntervalLengthInMinutes);
                            return new Price
                            {
                                Value = decimal.Parse(x.IntervalPrice),
                                ValidFrom = validFrom,
                                ValidTo = validTo
                            };
                        }).ToList();
                        
                        // Log first and last interval for this day to verify timezone handling
                        if (parsedPrices.Count > 0)
                        {
                            var firstPrice = parsedPrices.First();
                            var lastPrice = parsedPrices.Last();
                            _logger.LogDebug("Price intervals for {CurrentDate}: {FirstStart} UTC to {LastEnd} UTC ({Count} intervals)", 
                                currentDate.ToString("yyyy-MM-dd"),
                                firstPrice.ValidFrom.UtcDateTime, 
                                lastPrice.ValidTo.UtcDateTime,
                                parsedPrices.Count);
                        }
                        
                        prices.AddRange(parsedPrices);
                    }
                }
            }
            else
            {
                _logger.LogWarning("No price data returned from PGE API for date {CurrentDate}", currentDate.ToString("yyyy-MM-dd"));
            }
            
            currentDate = currentDate.AddDays(1);
            
            // Rate limit to avoid overwhelming the API
            if (currentDate <= endDate)
            {
                await Task.Delay(500);
            }
        }
        
        // Log all prices before filtering
        _logger.LogInformation("Retrieved {TotalCount} price intervals from PGE API", prices.Count);
        if (prices.Count > 0)
        {
            var minPrice = prices.Min(p => p.ValidFrom);
            var maxPrice = prices.Max(p => p.ValidTo);
            _logger.LogInformation("Price data coverage: {MinTime} UTC to {MaxTime} UTC", minPrice.UtcDateTime, maxPrice.UtcDateTime);
        }
        
        // Filter to return prices that overlap with the requested range
        // A price overlaps if it starts before the range ends AND ends after the range starts
        var filteredPrices = prices.Where(p => p.ValidFrom < to && p.ValidTo > from).ToList();
        
        _logger.LogInformation("Returning {Count} price intervals covering the requested period ({From} UTC to {To} UTC)", 
            filteredPrices.Count, from.UtcDateTime, to.UtcDateTime);
        
        // Check for gaps in coverage
        if (filteredPrices.Count > 1)
        {
            var sortedPrices = filteredPrices.OrderBy(p => p.ValidFrom).ToList();
            for (int i = 1; i < sortedPrices.Count; i++)
            {
                var prevEnd = sortedPrices[i - 1].ValidTo;
                var currentStart = sortedPrices[i].ValidFrom;
                if (prevEnd < currentStart)
                {
                    _logger.LogWarning("Gap in price data: {PrevEnd} UTC to {CurrentStart} UTC", 
                        prevEnd.UtcDateTime, currentStart.UtcDateTime);
                }
            }
        }
        
        foreach (var price in filteredPrices)
        {
            _logger.LogDebug("Price interval: {ValidFrom} UTC - {ValidTo} UTC: {Value}", 
                price.ValidFrom.UtcDateTime, price.ValidTo.UtcDateTime, price.Value);
        }
        
        return filteredPrices;
    }

    private static DateTimeOffset ParsePGEDateTime(string dateTimeString)
    {
        // PGE returns dates in multiple formats:
        // "2023-10-26T00:00:00-07:00" (with colon in timezone)
        // "2023-10-26T00:00:00-0700" (without colon in timezone)
        
        var formats = new[]
        {
            "yyyy-MM-ddTHH:mm:sszzz",     // -07:00 format
            "yyyy-MM-ddTHH:mm:sszz",      // -0700 format  
            "yyyy-MM-ddTHH:mm:ss.fffzzz", // with milliseconds and colon
            "yyyy-MM-ddTHH:mm:ss.fffzz"   // with milliseconds, no colon
        };
        
        if (DateTimeOffset.TryParseExact(
            dateTimeString,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var result))
        {
            return result;
        }
        
        // Fallback to standard parsing
        try
        {
            return DateTimeOffset.Parse(dateTimeString, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            throw new FormatException($"Unable to parse PGE datetime string: '{dateTimeString}'");
        }
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
