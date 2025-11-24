using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using TeslaMateAgile.Data;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Services.Interfaces;
using TimeZoneConverter;

namespace TeslaMateAgile.Services;

public class FixedPriceWeeklyService : IDynamicPriceDataService
{
    private readonly Dictionary<DayOfWeek, List<FixedPriceWeekly>> _fixedPrices;

    public FixedPriceWeeklyService(
        IOptions<FixedPriceWeeklyOptions> options
        )
    {
        if (!TZConvert.TryGetTimeZoneInfo(options.Value.TimeZone, out _timeZone))
        {
            throw new ArgumentException($"Invalid TimeZone {options.Value.TimeZone}", nameof(options.Value.TimeZone));
        }
        _fixedPrices = GetFixedPrices(options.Value);
    }

    public Task<PriceData> GetPriceData(DateTimeOffset from, DateTimeOffset to)
    {
        var prices = new List<Price>();

        // Get all days between the range inclusive

        var fromDate = from.Add(_timeZone.GetUtcOffset(from)).Date;
        var toDate = to.Add(_timeZone.GetUtcOffset(to)).Date;
        var days = new Dictionary<DateTimeOffset, List<Price>>();
        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            prices.AddRange(GetPriceDataForDate(date));
        }

        // Truncate the prices to the requested range, inclusive

        prices = prices.Where(x => x.ValidFrom < to && x.ValidTo > from).ToList();

        return Task.FromResult(new PriceData(prices));
    }

    private List<Price> GetPriceDataForDate(DateTime date)
    {
        // Get all fixed prices for the day

        var dateUtc = DateTime.SpecifyKind(date, DateTimeKind.Utc);

        var prices = new List<Price>();

        var fixedPricesForDay = _fixedPrices[date.DayOfWeek];

        decimal? crossoverPrice = null;
        foreach (var fixedPrice in fixedPricesForDay)
        {
            var validFrom = dateUtc;
            var validTo = dateUtc;

            if (fixedPrice.FromHour.HasValue && fixedPrice.FromMinute.HasValue && fixedPrice.ToHour.HasValue && fixedPrice.ToMinute.HasValue)
            {
                validFrom = validFrom.AddHours(fixedPrice.FromHour.Value).AddMinutes(fixedPrice.FromMinute.Value);
                validTo = validTo.AddHours(fixedPrice.ToHour.Value).AddMinutes(fixedPrice.ToMinute.Value);
            }
            else
            {
                validTo = validTo.AddDays(1);
            }

            // Handle the scenario where they cross midnight

            if (validFrom > validTo)
            {
                crossoverPrice = fixedPrice.Value;
                if (validTo == dateUtc)
                {
                    continue;
                }
                validFrom = dateUtc;
            }

            var price = new Price
            {
                ValidFrom = validFrom.Add(-_timeZone.GetUtcOffset(validFrom)),
                ValidTo = validTo.Add(-_timeZone.GetUtcOffset(validTo)),
                Value = fixedPrice.Value
            };
            prices.Add(price);
        }

        // Ensure we have the last price of the day to cover the entire day

        prices = prices.OrderBy(x => x.ValidFrom).ToList();

        if (crossoverPrice.HasValue)
        {
            var lastPrice = prices.Last();
            var validTo = DateTime.SpecifyKind(date.AddDays(1), DateTimeKind.Utc);
            var price = new Price
            {
                ValidFrom = lastPrice.ValidTo,
                ValidTo = validTo.Add(-_timeZone.GetUtcOffset(validTo)),
                Value = crossoverPrice.Value
            };
            prices.Add(price);
        }

        // Verify that the prices cover the entire day

        // Calculate the actual length of the day in this timezone (handles DST)
        var dayStart = dateUtc.Add(-_timeZone.GetUtcOffset(dateUtc));
        var dayEnd = dateUtc.AddDays(1).Add(-_timeZone.GetUtcOffset(dateUtc.AddDays(1)));

        if (prices.First().ValidFrom > dayStart)
        {
            throw new Exception("Invalid fixed price data, does not cover the entire day");
        }

        if (prices.Last().ValidTo < dayEnd)
        {
            throw new Exception("Invalid fixed price data, does not cover the entire day");
        }

        // Verify that the prices are continuous and do not overlap or have gaps

        for (var i = 1; i < prices.Count; i++)
        {
            if (prices[i - 1].ValidTo != prices[i].ValidFrom)
            {
                throw new Exception("Invalid fixed price data, prices are not continuous");
            }
        }

        return prices;
    }

    private class FixedPriceWeekly
    {
        public int? FromHour { get; set; }
        public int? FromMinute { get; set; }
        public int? ToHour { get; set; }
        public int? ToMinute { get; set; }
        public decimal Value { get; set; }
    }

    private static readonly Regex FixedPriceWeeklyRegex = new Regex("(?<days>[a-zA-Z,-]+)(=(?<fromHour>\\d\\d):(?<fromMinute>\\d\\d)-(?<toHour>\\d\\d):(?<toMinute>\\d\\d))?=(?<value>.+)");
    private readonly TimeZoneInfo _timeZone;

    private Dictionary<DayOfWeek, List<FixedPriceWeekly>> GetFixedPrices(FixedPriceWeeklyOptions options)
    {
        var fixedPricesDict = new Dictionary<DayOfWeek, List<FixedPriceWeekly>>();

        foreach (var price in options.Prices)
        {
            var match = FixedPriceWeeklyRegex.Match(price);
            if (!match.Success)
            {
                throw new ArgumentException($"Failed to parse fixed price: {price}", nameof(price));
            }

            var fromHour = match.Groups["fromHour"].Success ? int.Parse(match.Groups["fromHour"].Value) : (int?)null;
            var fromMinute = match.Groups["fromMinute"].Success ? int.Parse(match.Groups["fromMinute"].Value) : (int?)null;
            var toHour = match.Groups["toHour"].Success ? int.Parse(match.Groups["toHour"].Value) : (int?)null;
            var toMinute = match.Groups["toMinute"].Success ? int.Parse(match.Groups["toMinute"].Value) : (int?)null;

            if (!decimal.TryParse(match.Groups["value"].Value, out var value))
            {
                throw new ArgumentException($"Failed to parse fixed price value: {match.Groups["value"].Value}", nameof(value));
            }

            // Validate appropriate hour and minute values

            if (fromHour.HasValue && (fromHour < 0 || fromHour > 23))
            {
                throw new ArgumentException($"Invalid fromHour: {fromHour}", nameof(fromHour));
            }

            if (fromMinute.HasValue && (fromMinute < 0 || fromMinute > 59))
            {
                throw new ArgumentException($"Invalid fromMinute: {fromMinute}", nameof(fromMinute));
            }

            if (toHour.HasValue && (toHour < 0 || toHour > 23))
            {
                throw new ArgumentException($"Invalid toHour: {toHour}", nameof(toHour));
            }

            if (toMinute.HasValue && (toMinute < 0 || toMinute > 59))
            {
                throw new ArgumentException($"Invalid toMinute: {toMinute}", nameof(toMinute));
            }

            var days = ParseDays(match.Groups["days"].Value);

            var fixedPrice = new FixedPriceWeekly
            {
                FromHour = fromHour,
                FromMinute = fromMinute,
                ToHour = toHour,
                ToMinute = toMinute,
                Value = value
            };

            foreach (var day in days)
            {
                if (!fixedPricesDict.ContainsKey(day))
                {
                    fixedPricesDict[day] = new List<FixedPriceWeekly>();
                }
                fixedPricesDict[day].Add(fixedPrice);
            }
        }

        // Validate that all days of the week are covered
        var allDays = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>();
        if (!allDays.All(day => fixedPricesDict.ContainsKey(day)))
        {
            throw new ArgumentException("Invalid fixed price data, does not cover the entire week");
        }

        // Validate that each day covers the entire 24 hours
        foreach (var day in fixedPricesDict.Keys)
        {
            // if a full day one, skip
            if (fixedPricesDict[day].Any(x => !x.FromHour.HasValue || !x.FromMinute.HasValue || !x.ToHour.HasValue || !x.ToMinute.HasValue))
            {
                // Verify there are no other prices for the day
                if (fixedPricesDict[day].Count > 1)
                {
                    throw new ArgumentException("Invalid fixed price data, other prices specified for full day price");
                }
                continue;
            }

            var dayPrices = fixedPricesDict[day].OrderBy(x => x.FromHour).ThenBy(x => x.FromMinute).ToList();
            var totalHours = 0M;
            for (var i = 0; i < dayPrices.Count; i++)
            {
                var fromHours = dayPrices[i].FromHour.Value + (dayPrices[i].FromMinute.Value / 60M);
                var toHours = dayPrices[i].ToHour.Value + (dayPrices[i].ToMinute.Value / 60M);
                if (fromHours > toHours)
                {
                    toHours += 24;
                }
                totalHours += toHours - fromHours;
            }
            if (totalHours < 24)
            {
                throw new ArgumentException("Invalid fixed price data, does not cover the full 24 hours");
            }
            else if (totalHours > 24)
            {
                throw new ArgumentException("Invalid fixed price data, covers more than 24 hours");
            }
        }

        // Validate that the prices are continuous and do not overlap or have gaps
        foreach (var day in fixedPricesDict.Keys)
        {
            var dayPrices = fixedPricesDict[day].OrderBy(x => x.FromHour).ThenBy(x => x.FromMinute).ToList();
            for (var i = 1; i < dayPrices.Count; i++)
            {
                var previousPrice = dayPrices[i - 1];
                var currentPrice = dayPrices[i];
                if (previousPrice.ToHour != currentPrice.FromHour || previousPrice.ToMinute != currentPrice.FromMinute)
                {
                    throw new ArgumentException("Invalid fixed price data, prices are not continuous");
                }
            }
        }

        return fixedPricesDict;
    }

    private List<DayOfWeek> ParseDays(string days)
    {
        var dayList = new List<DayOfWeek>();
        var dayRanges = days.Split(',');

        foreach (var dayRange in dayRanges)
        {
            if (dayRange.Contains('-'))
            {
                var rangeParts = dayRange.Split('-');
                var startDay = ParseDay(rangeParts[0]);
                var endDay = ParseDay(rangeParts[1]);

                if (startDay <= endDay)
                {
                    for (var day = startDay; day <= endDay; day++)
                    {
                        dayList.Add(day);
                    }
                }
                else
                {
                    for (var day = startDay; day <= DayOfWeek.Saturday; day++)
                    {
                        dayList.Add(day);
                    }
                    for (var day = DayOfWeek.Sunday; day <= endDay; day++)
                    {
                        dayList.Add(day);
                    }
                }
            }
            else
            {
                dayList.Add(ParseDay(dayRange));
            }
        }

        return dayList;
    }

    private DayOfWeek ParseDay(string day)
    {
        return day.ToLower() switch
        {
            "mon" => DayOfWeek.Monday,
            "tue" => DayOfWeek.Tuesday,
            "wed" => DayOfWeek.Wednesday,
            "thu" => DayOfWeek.Thursday,
            "fri" => DayOfWeek.Friday,
            "sat" => DayOfWeek.Saturday,
            "sun" => DayOfWeek.Sunday,
            _ => throw new ArgumentException($"Invalid day: {day}", nameof(day))
        };
    }
}
