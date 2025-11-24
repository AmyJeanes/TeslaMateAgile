using Microsoft.Extensions.Options;
using NUnit.Framework;
using TeslaMateAgile.Data;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Services;

namespace TeslaMateAgile.Tests.Services;

public class FixedPriceServiceTests
{
    public FixedPriceService Setup(string timeZone, List<string> prices)
    {
        var options = Options.Create(new FixedPriceOptions { TimeZone = timeZone, Prices = prices });
        return new FixedPriceService(options);
    }

    private static readonly object[][] FixedPriceService_GetPriceData_Cases = new object[][] {
            new object[]
            {
                "PreviousNight",
                "Etc/UTC",
                new List<string>
                {
                    "08:00-13:00=0.015",
                    "13:00-20:00=0.05",
                    "20:00-03:30=0.04",
                    "03:30-06:00=0.035",
                    "06:00-08:00=0.02",
                },
                DateTimeOffset.Parse("2021-02-01T03:00:00Z"),
                DateTimeOffset.Parse("2021-02-01T18:00:00Z"),
                new List<Price>
                {
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-01-31T20:00:00Z"),
                        ValidTo = DateTimeOffset.Parse("2021-02-01T03:30:00Z"),
                        Value = 0.04M
                    },
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-02-01T03:30:00Z"),
                        ValidTo = DateTimeOffset.Parse("2021-02-01T06:00:00Z"),
                        Value = 0.035M
                    },
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-02-01T06:00:00Z"),
                        ValidTo = DateTimeOffset.Parse("2021-02-01T08:00:00Z"),
                        Value = 0.02M
                    },
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-02-01T08:00:00Z"),
                        ValidTo = DateTimeOffset.Parse("2021-02-01T13:00:00Z"),
                        Value = 0.015M
                    },
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-02-01T13:00:00Z"),
                        ValidTo = DateTimeOffset.Parse("2021-02-01T20:00:00Z"),
                        Value = 0.05M
                    }
                }
            },
            new object[]
            {
                "MidDay_EdgeFrom",
                "Etc/UTC",
                new List<string>
                {
                    "08:00-13:00=0.015",
                    "13:00-20:00=0.05",
                    "20:00-03:30=0.04",
                    "03:30-06:00=0.035",
                    "06:00-08:00=0.02",
                },
                DateTimeOffset.Parse("2021-02-01T08:00:00Z"),
                DateTimeOffset.Parse("2021-02-01T18:00:00Z"),
                new List<Price>
                {
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-02-01T08:00:00Z"),
                        ValidTo = DateTimeOffset.Parse("2021-02-01T13:00:00Z"),
                        Value = 0.015M
                    },
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-02-01T13:00:00Z"),
                        ValidTo = DateTimeOffset.Parse("2021-02-01T20:00:00Z"),
                        Value = 0.05M
                    }
                }
            },
            new object[]
            {
                "MidDay_EdgeTo",
                "Etc/UTC",
                new List<string>
                {
                    "08:00-13:00=0.015",
                    "13:00-20:00=0.05",
                    "20:00-03:30=0.04",
                    "03:30-06:00=0.035",
                    "06:00-08:00=0.02",
                },
                DateTimeOffset.Parse("2021-02-01T15:00:00Z"),
                DateTimeOffset.Parse("2021-02-01T20:00:00Z"),
                new List<Price>
                {
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-02-01T13:00:00Z"),
                        ValidTo = DateTimeOffset.Parse("2021-02-01T20:00:00Z"),
                        Value = 0.05M
                    }
                }
            },
            new object[]
            {
                "NextNight",
                "Etc/UTC",
                new List<string>
                {
                    "08:00-13:00=0.015",
                    "13:00-20:00=0.05",
                    "20:00-03:30=0.04",
                    "03:30-06:00=0.035",
                    "06:00-08:00=0.02",
                },
                DateTimeOffset.Parse("2021-02-01T15:00:00Z"),
                DateTimeOffset.Parse("2021-02-02T07:00:00Z"),
                new List<Price>
                {
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-02-01T13:00:00Z"),
                        ValidTo = DateTimeOffset.Parse("2021-02-01T20:00:00Z"),
                        Value = 0.05M
                    },
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-02-01T20:00:00Z"),
                        ValidTo = DateTimeOffset.Parse("2021-02-02T03:30:00Z"),
                        Value = 0.04M
                    },
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-02-02T03:30:00Z"),
                        ValidTo = DateTimeOffset.Parse("2021-02-02T06:00:00Z"),
                        Value = 0.035M
                    },
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-02-02T06:00:00Z"),
                        ValidTo = DateTimeOffset.Parse("2021-02-02T08:00:00Z"),
                        Value = 0.02M
                    }
                }
            },
            new object[]
            {
                "TimeZone",
                "America/New_York",
                new List<string>
                {
                    "08:00-13:00=0.015",
                    "13:00-20:00=0.05",
                    "20:00-03:30=0.04",
                    "03:30-06:00=0.035",
                    "06:00-08:00=0.02",
                },
                DateTimeOffset.Parse("2021-02-01T15:00:00Z"),
                DateTimeOffset.Parse("2021-02-02T07:00:00Z"),
                new List<Price>
                {
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-02-01T13:00:00Z"),
                        ValidTo = DateTimeOffset.Parse("2021-02-01T18:00:00Z"),
                        Value = 0.015M
                    },
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-02-01T18:00:00Z"),
                        ValidTo = DateTimeOffset.Parse("2021-02-02T01:00:00Z"),
                        Value = 0.05M
                    },
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-02-02T01:00:00Z"),
                        ValidTo = DateTimeOffset.Parse("2021-02-02T08:30:00Z"),
                        Value = 0.04M
                    }
                }
            },
            new object[]
            {
                "TimeZone_DayEarlier",
                "America/New_York",
                new List<string>
                {
                    "08:00-13:00=0.015",
                    "13:00-20:00=0.05",
                    "20:00-03:30=0.04",
                    "03:30-06:00=0.035",
                    "06:00-08:00=0.02",
                },
                DateTimeOffset.Parse("2021-02-01T03:00:00Z"),
                DateTimeOffset.Parse("2021-02-01T10:00:00Z"),
                new List<Price>
                {
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-01-31T20:00:00-05:00"),
                        ValidTo = DateTimeOffset.Parse("2021-02-01T03:30:00-05:00"),
                        Value = 0.04M
                    },
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-02-01T03:30:00-05:00"),
                        ValidTo = DateTimeOffset.Parse("2021-02-01T06:00:00-05:00"),
                        Value = 0.035M
                    }
                }
            },
            new object[]
            {
                "TimeZone_DayAfter",
                "America/New_York",
                new List<string>
                {
                    "08:00-13:00=0.015",
                    "13:00-20:00=0.05",
                    "20:00-03:30=0.04",
                    "03:30-06:00=0.035",
                    "06:00-08:00=0.02",
                },
                DateTimeOffset.Parse("2021-02-01T15:00:00Z"),
                DateTimeOffset.Parse("2021-02-01T23:00:00Z"),
                new List<Price>
                {
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-02-01T08:00:00-05:00"),
                        ValidTo = DateTimeOffset.Parse("2021-02-01T13:00:00-05:00"),
                        Value = 0.015M
                    },
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-02-01T13:00:00-05:00"),
                        ValidTo = DateTimeOffset.Parse("2021-02-01T20:00:00-05:00"),
                        Value = 0.05M
                    }
                }
            },
            new object[]
            {
                "CrossDay_TimeZone",
                "Europe/Paris",
                new List<string>
                {
                    "08:00-23:00=0.1799",
                    "23:00-02:00=0.1346",
                    "02:00-06:00=0.1095",
                    "06:00-08:00=0.1346"
                },
                DateTimeOffset.Parse("2021-01-26T00:39:45Z"),
                DateTimeOffset.Parse("2021-01-26T04:14:30Z"),
                new List<Price>
                {
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-01-25T22:00:00+00:00"),
                        ValidTo = DateTimeOffset.Parse("2021-01-26T01:00:00+00:00"),
                        Value = 0.1346M
                    },
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-01-26T01:00:00+00:00"),
                        ValidTo = DateTimeOffset.Parse("2021-01-26T05:00:00+00:00"),
                        Value = 0.1095M
                    }
                }
            },
            new object[]
            {
                "DaylightSavingsTime",
                "Europe/London",
                new List<string>
                {
                    "23:30-20:30=0.138",
                    "20:30-23:30=0.045"
                },
                DateTimeOffset.Parse("2021-04-13T19:46:13Z"),
                DateTimeOffset.Parse("2021-04-13T22:02:46Z"),
                new List<Price>
                {
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-04-13T20:30:00+01:00"),
                        ValidTo = DateTimeOffset.Parse("2021-04-13T23:30:00+01:00"),
                        Value = 0.045M
                    }
                }
            },
            new object[]
            {
                "TimeZone_DSTEdge",
                "Europe/London",
                new List<string>
                {
                    "00:30-04:30=0.0556",
                    "04:30-00:30=0.15"
                },
                DateTimeOffset.Parse("2021-06-16T23:00:51Z"),
                DateTimeOffset.Parse("2021-06-17T00:19:15Z"),
                new List<Price>
                {
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-06-16T03:30:00Z"),
                        ValidTo = DateTimeOffset.Parse("2021-06-16T23:30:00Z"),
                        Value = 0.15M
                    },
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-06-16T23:30:00Z"),
                        ValidTo = DateTimeOffset.Parse("2021-06-17T03:30:00Z"),
                        Value = 0.0556M
                    }
                }
            },
            new object[]
            {
                "TimeZone_SameDayEdge",
                "Europe/London",
                new List<string>
                {
                    "00:30-04:30=0.05",
                    "04:30-00:30=0.15"
                },
                DateTimeOffset.Parse("2021-07-01T00:09:14Z"),
                DateTimeOffset.Parse("2021-07-01T00:46:17Z"),
                new List<Price>
                {
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-06-30T23:30:00Z"),
                        ValidTo = DateTimeOffset.Parse("2021-07-01T03:30:00Z"),
                        Value = 0.05M
                    }
                }
            },
            new object[]
            {
                "TimeZone_CrossDay",
                "Europe/London",
                new List<string>
                {
                    "00:30-20:30=0.159",
                    "20:30-00:30=0.05"
                },
                DateTimeOffset.Parse("2021-06-22T19:38:30Z"),
                DateTimeOffset.Parse("2021-06-22T23:34:58Z"),
                new List<Price>
                {
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-06-22T19:30:00Z"),
                        ValidTo = DateTimeOffset.Parse("2021-06-22T23:30:00Z"),
                        Value = 0.05M
                    },
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-06-22T23:30:00Z"),
                        ValidTo = DateTimeOffset.Parse("2021-06-23T19:30:00Z"),
                        Value = 0.159M
                    }
                }
            }
        };

    [Test, TestCaseSource(nameof(FixedPriceService_GetPriceData_Cases))]
    public async Task FixedPriceService_GetPriceData(string testName, string timeZone, List<string> fixedPrices, DateTimeOffset from, DateTimeOffset to, List<Price> expectedPrices)
    {
        Console.WriteLine($"Running get price data test '{testName}'");
        var fixedPriceService = Setup(timeZone, fixedPrices);
        var result = await fixedPriceService.GetPriceData(from, to);
        var actualPrices = result.Prices.OrderBy(x => x.ValidFrom).ToList();
        Assert.That(expectedPrices.Count, Is.EqualTo(actualPrices.Count()));
        for (var i = 0; i < actualPrices.Count(); i++)
        {
            var actualPrice = actualPrices[i];
            var expectedPrice = expectedPrices[i];

            Assert.That(expectedPrice.ValidFrom, Is.EqualTo(actualPrice.ValidFrom));
            Assert.That(expectedPrice.ValidTo, Is.EqualTo(actualPrice.ValidTo));
            Assert.That(expectedPrice.Value, Is.EqualTo(actualPrice.Value));
        }
    }

    [Test]
    public void FixedPriceService_GetPriceData_InfiniteLoopProtection()
    {
        var from = DateTimeOffset.Parse("2021-04-01T00:00:00Z");
        var to = DateTimeOffset.Parse("2021-06-01T00:00:00Z");
        var fixedPrices = new List<string>
            {
                "00:30-20:30=0.159",
                "20:30-00:30=0.05"
            };
        var fixedPriceService = Setup("Europe/London", fixedPrices);
        var exception = Assert.ThrowsAsync<Exception>(async () => { await fixedPriceService.GetPriceData(from, to); });
        Assert.That(exception?.Message, Is.EqualTo("Infinite loop detected within FixedPrice provider"));
    }
}
