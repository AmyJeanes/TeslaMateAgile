using Microsoft.Extensions.Options;
using Moq;
using Moq.Contrib.HttpClient;
using NUnit.Framework;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Helpers.Interfaces;
using TeslaMateAgile.Services;

namespace TeslaMateAgile.Tests.Services;

public class EnerginetServiceTests
{
    private Mock<HttpMessageHandler> _handler;
    private Mock<IRateLimitHelper> _rateLimitHelper;

    [SetUp]
    public void Setup()
    {
        _handler = new Mock<HttpMessageHandler>();
        _rateLimitHelper = new Mock<IRateLimitHelper>();
    }

    private EnerginetService CreateSubject(FixedPriceOptions fixedPrices)
    {
        var httpClient = _handler.CreateClient();
        httpClient.BaseAddress = new Uri("https://api.energidataservice.dk/dataset/");
        var options = Options.Create(new EnerginetOptions
        {
            BaseUrl = "https://api.energidataservice.dk/dataset/",
            Region = EnerginetRegion.DK2,
            Currency = EnerginetCurrency.DKK,
            FixedPrices = fixedPrices
        });
        return new EnerginetService(httpClient, _rateLimitHelper.Object, options);
    }

    /// <summary>
    /// The day ahead dataset is quarter hourly. A fixed price that only applies to part of the
    /// day must therefore be applied to the records it actually covers, and to no others.
    /// </summary>
    [Test]
    public async Task GetPriceData_AppliesOneFixedPricePerRecord_AcrossAFixedPriceBoundary()
    {
        var json = File.ReadAllText(Path.Combine("Prices", "energinet_test.json"));
        _handler.SetupAnyRequest().ReturnsResponse(json, "application/json");

        // Danish grid tariffs. The 17:00 local boundary falls at 15:00 UTC during summer time.
        var subject = CreateSubject(new FixedPriceOptions
        {
            TimeZone = "Europe/Copenhagen",
            Prices = new List<string>
            {
                "00:00-06:00=0.229175",
                "06:00-17:00=0.282262",
                "17:00-21:00=0.537082",
                "21:00-24:00=0.282262",
            }
        });

        var priceData = await subject.GetPriceData(
            DateTimeOffset.Parse("2026-06-07T13:30:00Z"),
            DateTimeOffset.Parse("2026-06-07T15:30:00Z"));
        var prices = priceData.Prices.OrderBy(x => x.ValidFrom).ToList();

        // Spot prices in the fixture are zero, so each price is the fixed price alone.
        var expected = new (string TimeUTC, decimal Value)[]
        {
            ("2026-06-07T13:30:00Z", 0.282262M),
            ("2026-06-07T13:45:00Z", 0.282262M),
            ("2026-06-07T14:00:00Z", 0.282262M),
            ("2026-06-07T14:15:00Z", 0.282262M),
            ("2026-06-07T14:30:00Z", 0.282262M),
            ("2026-06-07T14:45:00Z", 0.282262M),
            ("2026-06-07T15:00:00Z", 0.537082M),
            ("2026-06-07T15:15:00Z", 0.537082M),
        };

        Assert.That(prices, Has.Count.EqualTo(expected.Length));
        for (var i = 0; i < expected.Length; i++)
        {
            var validFrom = DateTimeOffset.Parse(expected[i].TimeUTC);
            Assert.Multiple(() =>
            {
                Assert.That(prices[i].ValidFrom, Is.EqualTo(validFrom));
                Assert.That(prices[i].ValidTo, Is.EqualTo(validFrom.AddMinutes(15)));
                Assert.That(prices[i].Value, Is.EqualTo(expected[i].Value));
            });
        }
    }

    /// <summary>
    /// A fixed price boundary does not have to line up with the quarter hour grid, so a record
    /// it splits gets the two fixed prices weighted by how much of the record each one covers.
    /// </summary>
    [Test]
    public async Task GetPriceData_WeightsFixedPricesThatSplitARecord()
    {
        var json = File.ReadAllText(Path.Combine("Prices", "energinet_test.json"));
        _handler.SetupAnyRequest().ReturnsResponse(json, "application/json");

        // 15:40 local is 13:40 UTC, ten minutes into the 13:30 UTC record.
        var subject = CreateSubject(new FixedPriceOptions
        {
            TimeZone = "Europe/Copenhagen",
            Prices = new List<string>
            {
                "00:00-15:40=0.3",
                "15:40-24:00=0.6",
            }
        });

        var priceData = await subject.GetPriceData(
            DateTimeOffset.Parse("2026-06-07T13:30:00Z"),
            DateTimeOffset.Parse("2026-06-07T15:30:00Z"));
        var prices = priceData.Prices.OrderBy(x => x.ValidFrom).ToList();

        // (0.3 * 10 + 0.6 * 5) / 15
        Assert.That(prices[0].Value, Is.EqualTo(0.4M));
        Assert.That(prices[1].Value, Is.EqualTo(0.6M));
    }
}
