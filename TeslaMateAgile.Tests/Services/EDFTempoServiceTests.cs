using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Contrib.HttpClient;
using NUnit.Framework;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Data;
using TeslaMateAgile.Helpers.Interfaces;
using TeslaMateAgile.Services;

namespace TeslaMateAgile.Tests.Services;

public class EDFTempoServiceTests
{
    private const string DstTempoPayload = """
[
  { "dateJour": "2025-10-25", "codeJour": 1, "periode": "BLEU" },
  { "dateJour": "2025-10-26", "codeJour": 1, "periode": "BLEU" },
  { "dateJour": "2025-10-27", "codeJour": 1, "periode": "BLEU" }
]
""";

    private Mock<HttpMessageHandler> _handler;
    private Mock<IRateLimitHelper> _rateLimitHelper;
    private EDFTempoService _subject;

    [SetUp]
    public void Setup()
    {
        _handler = new Mock<HttpMessageHandler>();
        var httpClient = _handler.CreateClient();
        httpClient.BaseAddress = new Uri("https://tempo.test");

        _rateLimitHelper = new Mock<IRateLimitHelper>();
        var options = Options.Create(new EDFTempoOptions
        {
            BaseUrl = "https://tempo.test",
            BLUE_HC = 0.10m,
            BLUE_HP = 0.20m,
            WHITE_HC = 0.30m,
            WHITE_HP = 0.40m,
            RED_HC = 0.50m,
            RED_HP = 0.60m
        });

        var logger = new Mock<ILogger<EDFTempoService>>();
        _subject = new EDFTempoService(httpClient, _rateLimitHelper.Object, options, logger.Object);
    }

    [Test]
    public async Task GetPriceData_CoversDstFallbackWithoutGaps()
    {
        _handler.SetupAnyRequest().ReturnsResponse(DstTempoPayload, "application/json");

        // Match the integration window that exposed the DST fallback gap
        var from = DateTimeOffset.Parse("2025-10-26T21:00:10Z");
        var to = DateTimeOffset.Parse("2025-10-27T02:45:35Z");

        var priceData = await _subject.GetPriceData(from, to);
        var prices = priceData.Prices.OrderBy(p => p.ValidFrom).ToList();

        Assert.That(prices.First().ValidFrom, Is.EqualTo(from));
        Assert.That(prices.Last().ValidTo, Is.EqualTo(to));

        Assert.That(prices.Count, Is.GreaterThanOrEqualTo(2), "Expected at least two segments across the fallback window");

        for (var i = 1; i < prices.Count; i++)
        {
            var gap = prices[i].ValidFrom - prices[i - 1].ValidTo;
            Assert.That(gap, Is.LessThanOrEqualTo(TimeSpan.FromMilliseconds(1)), "Prices should be contiguous across DST fallback");
        }

        Assert.That(prices.Select(p => p.Value).Distinct().Single(), Is.EqualTo(0.10m), "Expected off-peak blue rate across fallback");
        _rateLimitHelper.Verify(x => x.AddRequest(), Times.Once);
    }

    [Test]
    public async Task GetPriceData_ReturnsPeakAndOffPeakForRegularDay()
    {
        const string tempoPayload = """
[
  { "dateJour": "2025-02-09", "codeJour": 1, "periode": "BLEU" },
  { "dateJour": "2025-02-10", "codeJour": 1, "periode": "BLEU" },
  { "dateJour": "2025-02-11", "codeJour": 1, "periode": "BLEU" }
]
""";

        _handler.SetupAnyRequest().ReturnsResponse(tempoPayload, "application/json");

        var from = DateTimeOffset.Parse("2025-02-10T04:00:00Z");
        var to = DateTimeOffset.Parse("2025-02-10T20:00:00Z");

        var priceData = await _subject.GetPriceData(from, to);
        var prices = priceData.Prices.OrderBy(p => p.ValidFrom).ToList();

        Assert.That(prices.First().ValidFrom, Is.EqualTo(from));
        Assert.That(prices.Last().ValidTo, Is.EqualTo(to));
        Assert.That(prices.Count, Is.EqualTo(2), "Expected one off-peak and one peak segment");

        Assert.That(prices[0].Value, Is.EqualTo(0.10m), "Off-peak should use BLUE_HC");
        Assert.That(prices[1].Value, Is.EqualTo(0.20m), "Peak should use BLUE_HP");

        _rateLimitHelper.Verify(x => x.AddRequest(), Times.Once);
    }

    [Test]
    public async Task GetPriceData_CoversDstSpringForwardWithoutOverlap()
    {
        const string tempoPayload = """
[
  { "dateJour": "2025-03-29", "codeJour": 1, "periode": "BLEU" },
  { "dateJour": "2025-03-30", "codeJour": 1, "periode": "BLEU" },
  { "dateJour": "2025-03-31", "codeJour": 1, "periode": "BLEU" }
]
""";

        _handler.SetupAnyRequest().ReturnsResponse(tempoPayload, "application/json");

        var from = DateTimeOffset.Parse("2025-03-29T23:00:00Z");
        var to = DateTimeOffset.Parse("2025-03-30T05:00:00Z");

        var priceData = await _subject.GetPriceData(from, to);
        var prices = priceData.Prices.OrderBy(p => p.ValidFrom).ToList();

        Assert.That(prices.First().ValidFrom, Is.EqualTo(from));
        Assert.That(prices.Last().ValidTo, Is.EqualTo(to));

        for (var i = 1; i < prices.Count; i++)
        {
            var delta = prices[i].ValidFrom - prices[i - 1].ValidTo;
            Assert.That(delta, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(-1)), "Prices should not overlap across DST spring-forward");
            Assert.That(delta, Is.LessThanOrEqualTo(TimeSpan.FromMilliseconds(1)), "Prices should stay contiguous across DST spring-forward");
        }

        var distinctValues = prices.Select(p => p.Value).Distinct().ToList();
        Assert.That(distinctValues, Does.Contain(0.10m), "Expected off-peak blue rate across spring-forward");
        Assert.That(distinctValues.Count, Is.LessThanOrEqualTo(2), "Spring-forward window should not create extra segments");
        _rateLimitHelper.Verify(x => x.AddRequest(), Times.Once);
    }

    [Test]
    public async Task GetPriceData_BuildsExpectedUrlWithBufferDay()
    {
        const string tempoPayload = """
[
  { "dateJour": "2025-02-09", "codeJour": 1, "periode": "BLEU" },
  { "dateJour": "2025-02-10", "codeJour": 1, "periode": "BLEU" }
]
""";

        var expectedUrl = "https://tempo.test?dateJour[]=2025-02-09&dateJour[]=2025-02-10&";
        _handler.SetupRequest(HttpMethod.Get, expectedUrl).ReturnsResponse(tempoPayload, "application/json");

        var from = DateTimeOffset.Parse("2025-02-10T04:00:00Z");
        var to = DateTimeOffset.Parse("2025-02-10T05:00:00Z");

        var priceData = await _subject.GetPriceData(from, to);
        var prices = priceData.Prices.ToList();

        Assert.That(prices, Is.Not.Empty);
        _rateLimitHelper.Verify(x => x.AddRequest(), Times.Once);
    }

    [Test]
    public async Task GetPriceData_UsesPreviousDayColorForEarlyHoursAndCurrentDayForPeak()
    {
        const string tempoPayload = """
[
  { "dateJour": "2025-01-15", "codeJour": 1, "periode": "BLEU" },
  { "dateJour": "2025-01-16", "codeJour": 2, "periode": "BLANC" },
  { "dateJour": "2025-01-17", "codeJour": 3, "periode": "ROUGE" }
]
""";

        _handler.SetupAnyRequest().ReturnsResponse(tempoPayload, "application/json");

        var from = DateTimeOffset.Parse("2025-01-16T00:30:00Z");
        var to = DateTimeOffset.Parse("2025-01-16T07:30:00Z");

        var priceData = await _subject.GetPriceData(from, to);
        var prices = priceData.Prices.OrderBy(p => p.ValidFrom).ToList();

        Assert.That(prices.Count, Is.EqualTo(2));
        Assert.That(prices[0].Value, Is.EqualTo(0.10m), "Early hours should use previous day's BLUE_HC");
        Assert.That(prices[1].Value, Is.EqualTo(0.40m), "Daytime should use current day's WHITE_HP");
        Assert.That(prices.First().ValidFrom, Is.EqualTo(from));
        Assert.That(prices.Last().ValidTo, Is.EqualTo(to));

        _rateLimitHelper.Verify(x => x.AddRequest(), Times.Once);
    }

    [Test]
    public void GetPriceData_PropagatesErrorAndCountsRateLimit()
    {
        _handler.SetupAnyRequest().ThrowsAsync(new HttpRequestException("tempo failure"));

        var from = DateTimeOffset.Parse("2025-02-10T04:00:00Z");
        var to = DateTimeOffset.Parse("2025-02-10T05:00:00Z");

        Assert.ThrowsAsync<HttpRequestException>(() => _subject.GetPriceData(from, to));
        _rateLimitHelper.Verify(x => x.AddRequest(), Times.Once);
    }
}
