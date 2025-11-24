using Microsoft.Extensions.Options;
using NUnit.Framework;
using NUnit.Framework.Internal;
using TeslaMateAgile.Data;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Services;

namespace TeslaMateAgile.Tests.Services
{
    [TestFixture]
    public class FixedPriceWeeklyServiceTests
    {
        private FixedPriceWeeklyService Setup(string timeZone, List<string> prices)
        {
            var options = Options.Create(new FixedPriceWeeklyOptions { TimeZone = timeZone, Prices = prices });
            return new FixedPriceWeeklyService(options);
        }

        private static readonly object[][] FixedPriceWeeklyService_GetPriceData_Cases = new object[][]
        {
            new object[]
            {
                "MondayToNextTuesday",
                "Europe/London",
                new List<string>
                {
                    "Mon-Fri=08:00-13:00=0.1559",
                    "Mon-Fri=13:00-20:00=0.05",
                    "Mon-Fri=20:00-03:30=0.04",
                    "Mon-Fri=03:30-06:00=0.035",
                    "Mon-Fri=06:00-08:00=0.02",
                    "Sat=18:00-20:00=0.05",
                    "Sat=20:00-18:00=0.025",
                    "Sun=0.042"
                },
                new DateTimeOffset(new DateTime(2023, 1, 2, 2, 0, 0, DateTimeKind.Utc)), // Monday
                new DateTimeOffset(new DateTime(2023, 1, 10, 4, 0, 0, DateTimeKind.Utc)), // Next Tuesday
                new List<Price>
                {
                    // Monday from 2am
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 2, 0, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 2, 3, 30, 0, DateTimeKind.Utc)), Value = 0.04m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 2, 3, 30, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 2, 6, 0, 0, DateTimeKind.Utc)), Value = 0.035m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 2, 6, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 2, 8, 0, 0, DateTimeKind.Utc)), Value = 0.02m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 2, 8, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 2, 13, 0, 0, DateTimeKind.Utc)), Value = 0.1559m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 2, 13, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 2, 20, 0, 0, DateTimeKind.Utc)), Value = 0.05m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 2, 20, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 3, 0, 0, 0, DateTimeKind.Utc)), Value = 0.04m },

                    // Tuesday
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 3, 0, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 3, 3, 30, 0, DateTimeKind.Utc)), Value = 0.04m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 3, 3, 30, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 3, 6, 0, 0, DateTimeKind.Utc)), Value = 0.035m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 3, 6, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 3, 8, 0, 0, DateTimeKind.Utc)), Value = 0.02m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 3, 8, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 3, 13, 0, 0, DateTimeKind.Utc)), Value = 0.1559m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 3, 13, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 3, 20, 0, 0, DateTimeKind.Utc)), Value = 0.05m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 3, 20, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 4, 0, 0, 0, DateTimeKind.Utc)), Value = 0.04m },

                    // Wednesday
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 4, 0, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 4, 3, 30, 0, DateTimeKind.Utc)), Value = 0.04m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 4, 3, 30, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 4, 6, 0, 0, DateTimeKind.Utc)), Value = 0.035m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 4, 6, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 4, 8, 0, 0, DateTimeKind.Utc)), Value = 0.02m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 4, 8, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 4, 13, 0, 0, DateTimeKind.Utc)), Value = 0.1559m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 4, 13, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 4, 20, 0, 0, DateTimeKind.Utc)), Value = 0.05m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 4, 20, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 5, 0, 0, 0, DateTimeKind.Utc)), Value = 0.04m },

                    // Thursday
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 5, 0, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 5, 3, 30, 0, DateTimeKind.Utc)), Value = 0.04m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 5, 3, 30, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 5, 6, 0, 0, DateTimeKind.Utc)), Value = 0.035m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 5, 6, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 5, 8, 0, 0, DateTimeKind.Utc)), Value = 0.02m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 5, 8, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 5, 13, 0, 0, DateTimeKind.Utc)), Value = 0.1559m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 5, 13, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 5, 20, 0, 0, DateTimeKind.Utc)), Value = 0.05m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 5, 20, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 6, 0, 0, 0, DateTimeKind.Utc)), Value = 0.04m },

                    // Friday
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 6, 0, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 6, 3, 30, 0, DateTimeKind.Utc)), Value = 0.04m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 6, 3, 30, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 6, 6, 0, 0, DateTimeKind.Utc)), Value = 0.035m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 6, 6, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 6, 8, 0, 0, DateTimeKind.Utc)), Value = 0.02m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 6, 8, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 6, 13, 0, 0, DateTimeKind.Utc)), Value = 0.1559m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 6, 13, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 6, 20, 0, 0, DateTimeKind.Utc)), Value = 0.05m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 6, 20, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 7, 0, 0, 0, DateTimeKind.Utc)), Value = 0.04m },

                    // Saturday
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 7, 0, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 7, 18, 0, 0, DateTimeKind.Utc)), Value = 0.025m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 7, 18, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 7, 20, 0, 0, DateTimeKind.Utc)), Value = 0.05m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 7, 20, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 8, 0, 0, 0, DateTimeKind.Utc)), Value = 0.025m },

                    // Sunday
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 8, 0, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 9, 0, 0, 0, DateTimeKind.Utc)), Value = 0.042m },

                    // Monday
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 9, 0, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 9, 3, 30, 0, DateTimeKind.Utc)), Value = 0.04m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 9, 3, 30, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 9, 6, 0, 0, DateTimeKind.Utc)), Value = 0.035m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 9, 6, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 9, 8, 0, 0, DateTimeKind.Utc)), Value = 0.02m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 9, 8, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 9, 13, 0, 0, DateTimeKind.Utc)), Value = 0.1559m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 9, 13, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 9, 20, 0, 0, DateTimeKind.Utc)), Value = 0.05m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 9, 20, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 10, 0, 0, 0, DateTimeKind.Utc)), Value = 0.04m },

                    // Tuesday until 4am
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 10, 0, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 10, 3, 30, 0, DateTimeKind.Utc)), Value = 0.04m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 10, 3, 30, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 10, 6, 0, 0, DateTimeKind.Utc)), Value = 0.035m }
                }
            },
            new object[]
            {
                "DayRangeWrappingAroundWeek",
                "Europe/London",
                new List<string>
                {
                    "Wed=22:00-02:00=0.05",
                    "Wed=02:00-22:00=0.06",
                    "Thu-Tue=0.04"
                },
                new DateTimeOffset(new DateTime(2023, 1, 2, 2, 0, 0, DateTimeKind.Utc)), // Monday
                new DateTimeOffset(new DateTime(2023, 1, 5, 4, 0, 0, DateTimeKind.Utc)), // Thursday
                new List<Price>
                {
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 2, 0, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 3, 0, 0, 0, DateTimeKind.Utc)), Value = 0.04m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 3, 0, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 4, 0, 0, 0, DateTimeKind.Utc)), Value = 0.04m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 4, 0, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 4, 2, 0, 0, DateTimeKind.Utc)), Value = 0.05m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 4, 2, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 4, 22, 0, 0, DateTimeKind.Utc)), Value = 0.06m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 4, 22, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 5, 0, 0, 0, DateTimeKind.Utc)), Value = 0.05m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 5, 0, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 6, 0, 0, 0, DateTimeKind.Utc)), Value = 0.04m },
                }
            },
            new object[]
            {
                "CommaSeparatedDays",
                "Europe/London",
                new List<string>
                {
                    "Mon-Wed=08:00-13:00=0.1559",
                    "Mon-Wed=13:00-20:00=0.05",
                    "Mon-Wed=20:00-08:00=0.04",
                    "Thu,Fri=08:00-18:00=0.1559",
                    "Thu,Fri=18:00-08:00=0.04",
                    "Sat=04:00-08:00=0.035",
                    "Sat=08:00-13:00=0.02",
                    "Sat=13:00-04:00=0.05",
                    "Sun=0.22"
                },
                new DateTimeOffset(new DateTime(2023, 1, 4, 2, 0, 0, DateTimeKind.Utc)), // Wed
                new DateTimeOffset(new DateTime(2023, 1, 7, 4, 0, 0, DateTimeKind.Utc)), // Sat
                new List<Price>
                {
                    // Wednesday
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 4, 0, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 4, 8, 0, 0, DateTimeKind.Utc)), Value = 0.04m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 4, 8, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 4, 13, 0, 0, DateTimeKind.Utc)), Value = 0.1559m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 4, 13, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 4, 20, 0, 0, DateTimeKind.Utc)), Value = 0.05m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 4, 20, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 5, 0, 0, 0, DateTimeKind.Utc)), Value = 0.04m },

                    // Thursday
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 5, 0, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 5, 8, 0, 0, DateTimeKind.Utc)), Value = 0.04m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 5, 8, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 5, 18, 0, 0, DateTimeKind.Utc)), Value = 0.1559m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 5, 18, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 6, 0, 0, 0, DateTimeKind.Utc)), Value = 0.04m },

                    // Friday
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 6, 0, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 6, 8, 0, 0, DateTimeKind.Utc)), Value = 0.04m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 6, 8, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 6, 18, 0, 0, DateTimeKind.Utc)), Value = 0.1559m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 6, 18, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 7, 0, 0, 0, DateTimeKind.Utc)), Value = 0.04m },

                    // Saturday
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 7, 0, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 7, 4, 0, 0, DateTimeKind.Utc)), Value = 0.05m }
                }
            },
            new object[]
            {
                "DifferentTimeZone",
                "America/New_York",
                new List<string>
                {
                    "Mon-Fri=08:00-13:00=0.1559",
                    "Mon-Fri=13:00-20:00=0.05",
                    "Mon-Fri=20:00-03:30=0.04",
                    "Mon-Fri=03:30-06:00=0.035",
                    "Mon-Fri=06:00-08:00=0.02",
                    "Sat=18:00-20:00=0.05",
                    "Sat=20:00-18:00=0.025",
                    "Sun=0.042"
                },
                new DateTimeOffset(new DateTime(2023, 1, 2, 2, 0, 0, DateTimeKind.Utc)), // Monday
                new DateTimeOffset(new DateTime(2023, 1, 4, 4, 0, 0, DateTimeKind.Utc)), // Wednesday
                new List<Price>
                {
                    // Monday
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 1, 5, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 2, 5, 0, 0, DateTimeKind.Utc)), Value = 0.042m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 2, 5, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 2, 8, 30, 0, DateTimeKind.Utc)), Value = 0.04m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 2, 8, 30, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 2, 11, 0, 0, DateTimeKind.Utc)), Value = 0.035m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 2, 11, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 2, 13, 0, 0, DateTimeKind.Utc)), Value = 0.02m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 2, 13, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 2, 18, 0, 0, DateTimeKind.Utc)), Value = 0.1559m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 2, 18, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 3, 1, 0, 0, DateTimeKind.Utc)), Value = 0.05m },

                    // Tuesday
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 3, 1, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 3, 5, 0, 0, DateTimeKind.Utc)), Value = 0.04m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 3, 5, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 3, 8, 30, 0, DateTimeKind.Utc)), Value = 0.04m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 3, 8, 30, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 3, 11, 0, 0, DateTimeKind.Utc)), Value = 0.035m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 3, 11, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 3, 13, 0, 0, DateTimeKind.Utc)), Value = 0.02m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 3, 13, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 3, 18, 0, 0, DateTimeKind.Utc)), Value = 0.1559m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 3, 18, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 4, 1, 0, 0, DateTimeKind.Utc)), Value = 0.05m },

                    // Wednesday
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2023, 1, 4, 1, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2023, 1, 4, 5, 0, 0, DateTimeKind.Utc)), Value = 0.04m }
                }
            },
            new object[]
            {
                "TimeZoneDSTEdge",
                "Europe/London",
                new List<string>
                {
                    "Mon-Fri=08:00-13:00=0.1559",
                    "Mon-Fri=13:00-08:00=0.234",
                    "Sat=18:00-20:00=0.05",
                    "Sat=20:00-18:00=0.025",
                    "Sun=01:00-02:00=0.012",
                    "Sun=02:00-01:00=0.044"
                },
                new DateTimeOffset(new DateTime(2024, 10, 25, 2, 0, 0, DateTimeKind.Utc)), // Friday before clocks go back
                new DateTimeOffset(new DateTime(2024, 10, 29, 4, 0, 0, DateTimeKind.Utc)), // Tuesday after clocks go back
                new List<Price>
                {
                    // Friday
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2024, 10, 24, 23, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2024, 10, 25, 7, 0, 0, DateTimeKind.Utc)), Value = 0.234m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2024, 10, 25, 7, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2024, 10, 25, 12, 0, 0, DateTimeKind.Utc)), Value = 0.1559m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2024, 10, 25, 12, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2024, 10, 25, 23, 0, 0, DateTimeKind.Utc)), Value = 0.234m },

                    // Saturday
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2024, 10, 25, 23, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2024, 10, 26, 17, 0, 0, DateTimeKind.Utc)), Value = 0.025m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2024, 10, 26, 17, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2024, 10, 26, 19, 0, 0, DateTimeKind.Utc)), Value = 0.05m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2024, 10, 26, 19, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2024, 10, 26, 23, 0, 0, DateTimeKind.Utc)), Value = 0.025m },

                    // Sunday (clocks go back 02:00->01:00)
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2024, 10, 26, 23, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2024, 10, 27, 1, 0, 0, DateTimeKind.Utc)), Value = 0.044m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2024, 10, 27, 1, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2024, 10, 27, 2, 0, 0, DateTimeKind.Utc)), Value = 0.012m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2024, 10, 27, 2, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2024, 10, 28, 0, 0, 0, DateTimeKind.Utc)), Value = 0.044m },

                    // Monday
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2024, 10, 28, 0, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2024, 10, 28, 8, 0, 0, DateTimeKind.Utc)), Value = 0.234m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2024, 10, 28, 8, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2024, 10, 28, 13, 0, 0, DateTimeKind.Utc)), Value = 0.1559m },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2024, 10, 28, 13, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2024, 10, 29, 0, 0, 0, DateTimeKind.Utc)), Value = 0.234m },

                    // Tuesday
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2024, 10, 29, 0, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2024, 10, 29, 8, 0, 0, DateTimeKind.Utc)), Value = 0.234m }
                }
            },
            new object[]
            {
                "CrossOverMidnight",
                "America/Denver",
                new List<string>
                {
                    "Mon-Fri=00:00-08:00=0.052004",
                    "Mon-Fri=08:00-10:00=0.253532",
                    "Mon-Fri=10:00-15:00=0.052004",
                    "Mon-Fri=15:00-20:00=0.253532",
                    "Mon-Fri=20:00-00:00=0.052004",
                    "Sat-Sun=0.052004"
                },
                new DateTimeOffset(new DateTime(2024, 11, 5, 19, 15, 12, DateTimeKind.Utc)),
                new DateTimeOffset(new DateTime(2024, 11, 5, 20, 50, 33, DateTimeKind.Utc)),
                new List<Price>
                {
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2024, 11, 5, 17, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2024, 11, 5, 22, 0, 0, DateTimeKind.Utc)), Value = 0.052004m },
                }
            },
            new object[]
            {
                "TimeZonePreviousDay",
                "America/Denver",
                new List<string>
                {
                    "Mon-Fri=00:00-08:00=0.052004",
                    "Mon-Fri=08:00-10:00=0.253532",
                    "Mon-Fri=10:00-15:00=0.052004",
                    "Mon-Fri=15:00-20:00=0.253532",
                    "Mon-Fri=20:00-00:00=0.052004",
                    "Sat-Sun=0.052004"
                },
                new DateTimeOffset(new DateTime(2024, 11, 4, 1, 22, 55, DateTimeKind.Utc)),
                new DateTimeOffset(new DateTime(2024, 11, 4, 1, 40, 58, DateTimeKind.Utc)),
                new List<Price>
                {
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2024, 11, 3, 6, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2024, 11, 4, 7, 0, 0, DateTimeKind.Utc)), Value = 0.052004m },
                }
            },
            new object[]
            {
                "DaylightSavingsTimeTransition",
                "Europe/Madrid",
                new List<string>
                {
                    "Mon-Fri=00:00-08:00=0.0925",
                    "Mon-Fri=08:00-10:00=0.1329",
                    "Mon-Fri=10:00-14:00=0.2220",
                    "Mon-Fri=14:00-18:00=0.1329",
                    "Mon-Fri=18:00-22:00=0.2220",
                    "Mon-Fri=22:00-00:00=0.1329",
                    "Sat,Sun=0.0925"
                },
                new DateTimeOffset(new DateTime(2025, 3, 30, 14, 08, 57, DateTimeKind.Utc)),
                new DateTimeOffset(new DateTime(2025, 3, 31, 05, 03, 22, DateTimeKind.Utc)),
                new List<Price>
                {
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2025, 3, 29, 23, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2025, 3, 30, 22, 0, 0, DateTimeKind.Utc)), Value = 0.0925M },
                    new() { ValidFrom = new DateTimeOffset(new DateTime(2025, 3, 30, 22, 0, 0, DateTimeKind.Utc)), ValidTo = new DateTimeOffset(new DateTime(2025, 3, 31, 6, 0, 0, DateTimeKind.Utc)), Value = 0.0925M }
                }
            }
        };

        [Test, TestCaseSource(nameof(FixedPriceWeeklyService_GetPriceData_Cases))]
        public async Task FixedPriceWeeklyService_GetPriceData(string testName, string timeZone, List<string> fixedPrices, DateTimeOffset from, DateTimeOffset to, List<Price> expectedPrices)
        {
            Console.WriteLine($"Running get price data test '{testName}'");
            var fixedPriceWeeklyService = Setup(timeZone, fixedPrices);
            var result = await fixedPriceWeeklyService.GetPriceData(from, to);
            var actualPrices = result.Prices.OrderBy(x => x.ValidFrom).ToList();
            Assert.That(actualPrices.Count(), Is.EqualTo(expectedPrices.Count));
            for (var i = 0; i < actualPrices.Count(); i++)
            {
                var actualPrice = actualPrices[i];
                var expectedPrice = expectedPrices[i];

                Assert.That(actualPrice.ValidFrom, Is.EqualTo(expectedPrice.ValidFrom));
                Assert.That(actualPrice.ValidTo, Is.EqualTo(expectedPrice.ValidTo));
                Assert.That(actualPrice.Value, Is.EqualTo(expectedPrice.Value));
            }
        }


        [Test]
        public void FixedPriceWeeklyService_InvalidDays()
        {
            var fixedPrices = new List<string>
            {
                "Mon-Fri=08:00-13:00=0.1559",
                "Mon-Fri=13:00-20:00=0.05",
                "Mon-Fri=20:00-03:30=0.04",
                "Mon-Fri=03:30-06:00=0.035",
                "Mon-Fri=06:00-08:00=0.02",
                "Sat=18:00-20:00=0.05",
                "Sat=20:00-18:00=0.025",
                "InvalidDay=0.042"
            };
            var exception = Assert.Throws<ArgumentException>(() => Setup("Europe/London", fixedPrices));
            Assert.That(exception?.Message, Is.EqualTo("Invalid day: InvalidDay (Parameter 'day')"));
        }

        [Test]
        public void FixedPriceWeeklyService_IncompleteWeekDays()
        {
            var fixedPrices = new List<string>
            {
                "Mon-Fri=08:00-13:00=0.1559",
                "Mon-Fri=13:00-20:00=0.05",
                "Mon-Fri=20:00-03:30=0.04",
                "Mon-Fri=03:30-06:00=0.035",
                "Mon-Fri=06:00-08:00=0.02",
                "Sat=18:00-20:00=0.05",
                "Sat=20:00-18:00=0.025"
            };
            var exception = Assert.Throws<ArgumentException>(() => Setup("Europe/London", fixedPrices));
            Assert.That(exception?.Message, Is.EqualTo("Invalid fixed price data, does not cover the entire week"));
        }

        [Test]
        public void FixedPriceWeeklyService_IncompleteDayHours()
        {
            var fixedPrices = new List<string>
            {
                "Mon-Fri=08:00-13:00=0.1559",
                "Mon-Fri=13:00-20:00=0.05",
                "Mon-Fri=20:00-03:30=0.04",
                "Mon-Fri=03:30-06:00=0.035",
                "Sat=18:00-20:00=0.05",
                "Sat=20:00-18:00=0.025",
                "Sun=0.042"
            };
            var exception = Assert.Throws<ArgumentException>(() => Setup("Europe/London", fixedPrices));
            Assert.That(exception?.Message, Is.EqualTo("Invalid fixed price data, does not cover the full 24 hours"));
        }

        [Test]
        public void FixedPriceWeeklyService_TooManyDayHours()
        {
            var fixedPrices = new List<string>
            {
                "Mon-Fri=08:00-13:00=0.1559",
                "Mon-Fri=13:00-20:00=0.05",
                "Mon-Fri=20:00-03:30=0.04",
                "Mon-Fri=03:30-06:00=0.035",
                "Mon-Fri=06:00-08:00=0.02",
                "Sat=18:00-20:00=0.05",
                "Sat=20:00-18:00=0.025",
                "Sun=0.042",
                "Mon-Fri=07:00-08:00=0.01",
                "Mon-Fri=08:00-09:00=0.01"
            };
            var exception = Assert.Throws<ArgumentException>(() => Setup("Europe/London", fixedPrices));
            Assert.That(exception?.Message, Is.EqualTo("Invalid fixed price data, covers more than 24 hours"));
        }

        [Test]
        public void FixedPriceWeeklyService_EmptyPricesList()
        {
            var fixedPrices = new List<string>();
            var exception = Assert.Throws<ArgumentException>(() => Setup("Europe/London", fixedPrices));
            Assert.That(exception?.Message, Is.EqualTo("Invalid fixed price data, does not cover the entire week"));
        }

        [Test]
        public void FixedPriceWeeklyService_InvalidTimeZone()
        {
            var fixedPrices = new List<string>
            {
                "Mon-Fri=08:00-13:00=0.1559"
            };
            var exception = Assert.Throws<ArgumentException>(() => Setup("Invalid/TimeZone", fixedPrices));
            Assert.That(exception?.Message, Is.EqualTo("Invalid TimeZone Invalid/TimeZone (Parameter 'TimeZone')"));
        }

        [Test]
        public void FixedPriceWeeklyService_InvalidTimeFormat()
        {
            var fixedPrices = new List<string>
            {
                "Mon=08:00-13:00=0.1559",
                "Mon=invalid-18:00=0.05"
            };
            var exception = Assert.Throws<ArgumentException>(() => Setup("Europe/London", fixedPrices));
            Assert.That(exception?.Message, Is.EqualTo("Failed to parse fixed price value: invalid-18:00=0.05 (Parameter 'value')"));
        }

        [Test]
        public void FixedPriceWeeklyService_OverlappingTimeRanges()
        {
            var fixedPrices = new List<string>
            {
                "Mon=08:00-13:00=0.1559",
                "Mon=12:00-18:00=0.05",
                "Mon=18:00-07:00=0.04",
                "Tue-Sun=0.04"
            };
            var exception = Assert.Throws<ArgumentException>(() => Setup("Europe/London", fixedPrices));
            Assert.That(exception?.Message, Is.EqualTo("Invalid fixed price data, prices are not continuous"));
        }

        [Test]
        public void FixedPriceWeeklyService_InvalidPriceFormat()
        {
            var fixedPrices = new List<string>
            {
                "Mon=08:00-13:00=invalid"
            };
            var exception = Assert.Throws<ArgumentException>(() => Setup("Europe/London", fixedPrices));
            Assert.That(exception?.Message, Is.EqualTo("Failed to parse fixed price value: invalid (Parameter 'value')"));
        }

        [Test]
        public void FixedPriceWeeklyService_InvalidTimeValues()
        {
            var fixedPrices = new List<string>
            {
                "Tue=08:60-09:00=0.05",
                "Tue=09:00-08:60=0.04",
                "Wed-Mon=0.10"
            };
            var exception = Assert.Throws<ArgumentException>(() => Setup("Europe/London", fixedPrices));
            Assert.That(exception?.Message, Is.EqualTo("Invalid fromMinute: 60 (Parameter 'fromMinute')"));
        }
    }
}
