using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TeslaMateAgile.Data;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Helpers.Interfaces;
using TeslaMateAgile.Services.Interfaces;

namespace TeslaMateAgile.Services
{
    public class EDFTempoService : IDynamicPriceDataService
    {
        private readonly HttpClient _client;
        private readonly IRateLimitHelper _rateLimitHelper;
        private readonly EDFTempoOptions _options;
        private readonly ILogger _logger;

        private readonly TimeZoneInfo frenchTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");

        public EDFTempoService(HttpClient client, IRateLimitHelper rateLimitHelper, IOptions<EDFTempoOptions> options, ILogger<EDFTempoService> logger)
        {
            _client = client;
            _rateLimitHelper = rateLimitHelper;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<PriceData> GetPriceData(DateTimeOffset from, DateTimeOffset to)
        {

            from = TimeZoneInfo.ConvertTime(from, frenchTimeZone);
            to = TimeZoneInfo.ConvertTime(to, frenchTimeZone);

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
            var schedule = new List<Tuple<DateTimeOffset, Tuple<TimeSpan, TimeSpan, int, int>, int>>();

            // Period Tuple: Start DateTime, End DateTime, color day associated (-1 = previous day), PeakHours (0=Off peak)
            var segments = new List<Tuple<TimeSpan, TimeSpan, int, int>>()
            {
                new Tuple<TimeSpan, TimeSpan, int, int>(TimeSpan.FromHours(0)+TimeSpan.FromMinutes(0)+TimeSpan.FromSeconds(0), TimeSpan.FromHours(5)+TimeSpan.FromMinutes(59)+TimeSpan.FromSeconds(59)+TimeSpan.FromMilliseconds(999), -1, 0),
                new Tuple<TimeSpan, TimeSpan, int, int>(TimeSpan.FromHours(6)+TimeSpan.FromMinutes(0)+TimeSpan.FromSeconds(0), TimeSpan.FromHours(21)+TimeSpan.FromMinutes(59)+TimeSpan.FromSeconds(59)+TimeSpan.FromMilliseconds(999), 0, 1),
                new Tuple<TimeSpan, TimeSpan, int, int>(TimeSpan.FromHours(22)+TimeSpan.FromMinutes(0)+TimeSpan.FromSeconds(0), TimeSpan.FromHours(23)+TimeSpan.FromMinutes(59)+TimeSpan.FromSeconds(59)+TimeSpan.FromMilliseconds(999), 0, 0)
            };

            // Price for each period
            var prices = new Dictionary<int, decimal>()
            {
                {0, _options.BLUE_HC}, {1, _options.BLUE_HP}, {2, _options.WHITE_HC}, {3, _options.WHITE_HP}, {4, _options.RED_HC}, {5, _options.RED_HP}
            };

            // For each day, add schedule (Peak/Off peak hours)
            for (var day = 1; day < data.Count; day++)
            {
                var dateLocal = DateTime.ParseExact(data[day].DateJour, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var date = new DateTimeOffset(dateLocal, frenchTimeZone.GetUtcOffset(dateLocal));

                foreach (var segment in segments)
                {
                    var codeJour = data[day + segment.Item3].CodeJour;
                    schedule.Add(new Tuple<DateTimeOffset, Tuple<TimeSpan, TimeSpan, int, int>, int>(date, segment, codeJour));
                }
            }

            // When have we charged?
            int startSchedule = 0, stopSchedule = 0;

            for (var i = 0; i < schedule.Count; i++)
            {
                if (fromDatetime.Date == schedule[i].Item1.Date && fromDatetime.TimeOfDay >= schedule[i].Item2.Item1 && fromDatetime.TimeOfDay <= schedule[i].Item2.Item2)
                {
                    startSchedule = i;
                }

                if (toDatetime.Date == schedule[i].Item1.Date && toDatetime.TimeOfDay >= schedule[i].Item2.Item1 && toDatetime.TimeOfDay <= schedule[i].Item2.Item2)
                {
                    stopSchedule = i;
                }
            }

            _logger.LogDebug("EDF: startSchedule: {startSchedule}, stopSchedule: {stopSchedule}", startSchedule, stopSchedule);

            // Get price
            for (var iter = startSchedule; iter <= stopSchedule; iter++)
            {
                var price = prices[(schedule[iter].Item3 - 1) * 2 + schedule[iter].Item2.Item4];

                if (iter == startSchedule && iter == stopSchedule)
                {
                    schedList.Add(new Price { ValidFrom = fromDatetime.ToUniversalTime(), ValidTo = toDatetime.ToUniversalTime(), Value = price });
                    break;
                }

                if (iter == startSchedule)
                {
                    schedList.Add(new Price { ValidFrom = fromDatetime.ToUniversalTime(), ValidTo = (schedule[iter].Item1.Add(schedule[iter].Item2.Item2)).ToUniversalTime(), Value = price });
                    continue;
                }

                if (iter == stopSchedule)
                {
                    schedList.Add(new Price { ValidFrom = (schedule[iter].Item1.Add(schedule[iter].Item2.Item1)).ToUniversalTime(), ValidTo = toDatetime.ToUniversalTime(), Value = price });
                    continue;
                }

                schedList.Add(new Price { ValidFrom = (schedule[iter].Item1.Add(schedule[iter].Item2.Item1)).ToUniversalTime(), ValidTo = (schedule[iter].Item1.Add(schedule[iter].Item2.Item2)).ToUniversalTime(), Value = price });
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
