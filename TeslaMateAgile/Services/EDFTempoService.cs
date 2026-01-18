using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TeslaMateAgile.Data;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Helpers.Interfaces;
using TeslaMateAgile.Services.Interfaces;
using TimeZoneConverter;

namespace TeslaMateAgile.Services
{
    public class EDFTempoService : IDynamicPriceDataService
    {
        private readonly HttpClient _client;
        private readonly IRateLimitHelper _rateLimitHelper;
        private readonly EDFTempoOptions _options;
        private readonly ILogger _logger;

        private readonly TimeZoneInfo _frenchTimeZone = TZConvert.GetTimeZoneInfo("Europe/Paris");

        public EDFTempoService(HttpClient client, IRateLimitHelper rateLimitHelper, IOptions<EDFTempoOptions> options, ILogger<EDFTempoService> logger)
        {
            _client = client;
            _rateLimitHelper = rateLimitHelper;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<PriceData> GetPriceData(DateTimeOffset from, DateTimeOffset to)
        {

            from = TimeZoneInfo.ConvertTime(from, _frenchTimeZone);
            to = TimeZoneInfo.ConvertTime(to, _frenchTimeZone);

            _logger.LogDebug("EDF: Range - {from} -> {to}", from, to);

            var days = "";
            // We need also data of previous day, as the ending off peak period end at 6AM the charge start day
            DateTimeOffset currentDate = from.Date.AddDays(-1);

            // Create URL
            while (currentDate <= to.Date)
            {
                days += $"dateJour[]={currentDate:yyyy-MM-dd}&";
                currentDate = currentDate.AddDays(1);
            }

            var url = $"{_options.BaseUrl}?{days}";

            _logger.LogDebug("EDF: URL: {url}", url);

            _rateLimitHelper.AddRequest();
            var response = await _client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var jsonResponse = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("EDF Response: {jsonResponse}", jsonResponse);

            var data = JsonSerializer.Deserialize<List<TempoDay>>(jsonResponse);

            if (data == null || data.Count == 0)
            {
                throw new Exception("Failed to retrieve or deserialize EDF Tempo API response");
            }

            foreach (var item in data)
            {
                _logger.LogDebug("EDF: TempoDay - Date: {item.dateJour}, Color: {item.codeJour}", item.DateJour, item.CodeJour);
            }

            return new PriceData(GenerateSchedule(data, from, to));
        }

        private IEnumerable<Price> GenerateSchedule(List<TempoDay> data, DateTimeOffset fromDatetime, DateTimeOffset toDatetime)
        {
            var schedList = new List<Price>();

            // Period Tuple: Start, End, color day associated (-1 = previous day), PeakHours (0 = Off peak)
            var segments = new List<Tuple<TimeSpan, TimeSpan, int, int>>()
            {
                new(TimeSpan.FromHours(0), TimeSpan.FromHours(5) + TimeSpan.FromMinutes(59) + TimeSpan.FromSeconds(59) + TimeSpan.FromMilliseconds(999), -1, 0),
                new(TimeSpan.FromHours(6), TimeSpan.FromHours(21) + TimeSpan.FromMinutes(59) + TimeSpan.FromSeconds(59) + TimeSpan.FromMilliseconds(999), 0, 1),
                new(TimeSpan.FromHours(22), TimeSpan.FromHours(23) + TimeSpan.FromMinutes(59) + TimeSpan.FromSeconds(59) + TimeSpan.FromMilliseconds(999), 0, 0)
            };

            // Price for each period
            var prices = new Dictionary<int, decimal>()
            {
                {0, _options.BLUE_HC}, {1, _options.BLUE_HP}, {2, _options.WHITE_HC}, {3, _options.WHITE_HP}, {4, _options.RED_HC}, {5, _options.RED_HP}
            };

            var schedule = new List<(DateTimeOffset StartUtc, DateTimeOffset EndUtc, int CodeJour, int PeakFlag)>();

            // For each day, add schedule (Peak/Off peak hours) with per-segment DST-aware offsets
            for (var day = 1; day < data.Count; day++)
            {
                var dateLocal = DateTime.ParseExact(data[day].DateJour, "yyyy-MM-dd", CultureInfo.InvariantCulture);

                foreach (var segment in segments)
                {
                    var segmentDateLocal = dateLocal;
                    var startLocal = segmentDateLocal.Add(segment.Item1);
                    var endLocal = segmentDateLocal.Add(segment.Item2);

                    var startOffset = _frenchTimeZone.GetUtcOffset(startLocal);
                    var endOffset = _frenchTimeZone.GetUtcOffset(endLocal);

                    var startUtc = new DateTimeOffset(startLocal, startOffset).ToUniversalTime();
                    var endUtc = new DateTimeOffset(endLocal, endOffset).ToUniversalTime();

                    var codeJour = data[day + segment.Item3].CodeJour;
                    schedule.Add((startUtc, endUtc, codeJour, segment.Item4));
                }
            }

            schedule = schedule.OrderBy(x => x.StartUtc).ToList();

            _logger.LogDebug("EDF: Built {Count} schedule segments", schedule.Count);
            foreach (var segment in schedule)
            {
                _logger.LogDebug("EDF: Segment {Start} -> {End} UTC, code {CodeJour}, peakFlag {PeakFlag}", segment.StartUtc, segment.EndUtc, segment.CodeJour, segment.PeakFlag);
            }

            // When have we charged?
            int startSchedule = -1, stopSchedule = -1;

            for (var i = 0; i < schedule.Count; i++)
            {
                if (fromDatetime.ToUniversalTime() >= schedule[i].StartUtc && fromDatetime.ToUniversalTime() <= schedule[i].EndUtc)
                {
                    startSchedule = i;
                }

                if (toDatetime.ToUniversalTime() >= schedule[i].StartUtc && toDatetime.ToUniversalTime() <= schedule[i].EndUtc)
                {
                    stopSchedule = i;
                }
            }

            if (startSchedule == -1 || stopSchedule == -1)
            {
                _logger.LogError("EDF: Could not locate schedule for range {From} -> {To}", fromDatetime, toDatetime);
                throw new Exception($"Unable to locate tempo schedule for range {fromDatetime} -> {toDatetime}");
            }

            _logger.LogDebug("EDF: startSchedule: {startSchedule}, stopSchedule: {stopSchedule}", startSchedule, stopSchedule);

            // Get price
            for (var iter = startSchedule; iter <= stopSchedule; iter++)
            {
                var price = prices[(schedule[iter].CodeJour - 1) * 2 + schedule[iter].PeakFlag];

                if (iter == startSchedule && iter == stopSchedule)
                {
                    schedList.Add(new Price { ValidFrom = fromDatetime.ToUniversalTime(), ValidTo = toDatetime.ToUniversalTime(), Value = price });
                    break;
                }

                if (iter == startSchedule)
                {
                    schedList.Add(new Price { ValidFrom = fromDatetime.ToUniversalTime(), ValidTo = schedule[iter].EndUtc, Value = price });
                    continue;
                }

                if (iter == stopSchedule)
                {
                    schedList.Add(new Price { ValidFrom = schedule[iter].StartUtc, ValidTo = toDatetime.ToUniversalTime(), Value = price });
                    continue;
                }

                schedList.Add(new Price { ValidFrom = schedule[iter].StartUtc, ValidTo = schedule[iter].EndUtc, Value = price });
            }

            foreach (var item in schedList)
            {
                _logger.LogDebug("EDF: Price: {item.ValidFrom}, {item.ValidTo}, {item.Value}", item.ValidFrom, item.ValidTo, item.Value);
            }

            return schedList;
        }
    }

    // JSON answer format of service provider
    public class TempoDay
    {
        [JsonPropertyName("dateJour")]
        public string DateJour { get; set; }
        [JsonPropertyName("codeJour")]
        public int CodeJour { get; set; }
        [JsonPropertyName("periode")]
        public string Periode { get; set; }
    }
}
