using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using TeslaMateAgile.Data;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Services.Interfaces;
using TimeZoneConverter;

namespace TeslaMateAgile.Services;

public class FixedPriceService : IDynamicPriceDataService
{
    private readonly List<FixedPrice> _fixedPrices;

    public FixedPriceService(
        IOptions<FixedPriceOptions> options
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
        var started = false;
        var dayIndex = -1;
        int fpIndex = 0;
        var maxIterations = 100; // fail-safe against infinite loop
        for (var i = 0; i <= maxIterations; i++)
        {
            var fixedPrice = _fixedPrices[fpIndex];
            var validFrom = DateTime.SpecifyKind(from.Date.AddDays(dayIndex).AddHours(fixedPrice.FromHour).AddMinutes(fixedPrice.FromMinute), DateTimeKind.Utc);
            var validTo = DateTime.SpecifyKind(from.Date.AddDays(dayIndex).AddHours(fixedPrice.ToHour).AddMinutes(fixedPrice.ToMinute), DateTimeKind.Utc);
            var price = new Price
            {
                ValidFrom = validFrom.Add(-_timeZone.GetUtcOffset(validFrom)),
                ValidTo = validTo.Add(-_timeZone.GetUtcOffset(validTo)),
                Value = fixedPrice.Value
            };
            if (price.ValidFrom < to && price.ValidTo > from)
            {
                prices.Add(price);
                started = true;
            }
            else if (started)
            {
                break;
            }
            fpIndex++;
            if (fpIndex >= _fixedPrices.Count)
            {
                fpIndex = 0;
                dayIndex++;
            }
            if (i == maxIterations)
            {
                throw new Exception("Infinite loop detected within FixedPrice provider");
            }
        }

        return Task.FromResult(new PriceData(prices.AsEnumerable()));
    }

    private class FixedPrice
    {
        public int FromHour { get; set; }
        public int FromMinute { get; set; }
        public int ToHour { get; set; }
        public int ToMinute { get; set; }
        public decimal Value { get; set; }
    }

    private static readonly Regex FixedPriceRegex = new Regex("(\\d\\d):(\\d\\d)-(\\d\\d):(\\d\\d)=(.+)");
    private readonly TimeZoneInfo _timeZone;

    private List<FixedPrice> GetFixedPrices(FixedPriceOptions options)
    {
        var fixedPrices = new List<FixedPrice>();
        var totalHours = 0M;
        FixedPrice lastFixedPrice = null;

        foreach (var price in options.Prices.OrderBy(x => x))
        {
            var match = FixedPriceRegex.Match(price);
            if (!match.Success) { throw new ArgumentException($"Failed to parse fixed price: {price}", nameof(price)); }
            var fromHour = int.Parse(match.Groups[1].Value);
            var fromMinute = int.Parse(match.Groups[2].Value);
            var toHour = int.Parse(match.Groups[3].Value);
            var toMinute = int.Parse(match.Groups[4].Value);
            if (!decimal.TryParse(match.Groups[5].Value, out var value))
            {
                throw new ArgumentException(nameof(value), $"Failed to parse fixed price value: {match.Groups[5].Value}");
            }
            var fromHours = fromHour + (fromMinute / 60M);
            var toHours = toHour + (toMinute / 60M);
            if (fromHours > toHours)
            {
                toHours += 24;
                toHour += 24;
            }
            var fixedPrice = new FixedPrice
            {
                FromHour = fromHour,
                FromMinute = fromMinute,
                ToHour = toHour,
                ToMinute = toMinute,
                Value = value
            };
            fixedPrices.Add(fixedPrice);

            if (lastFixedPrice != null && (fixedPrice.FromHour != lastFixedPrice.ToHour || fixedPrice.FromMinute != lastFixedPrice.ToMinute))
            {
                throw new ArgumentException($"Price from time does not match previous to time: {price}", nameof(price));
            }
            totalHours += toHours - fromHours;
            lastFixedPrice = fixedPrice;
        }
        if (totalHours != 24)
        {
            throw new ArgumentException($"Total hours do not equal 24, currently {totalHours}", nameof(totalHours));
        }
        return fixedPrices;
    }
}
