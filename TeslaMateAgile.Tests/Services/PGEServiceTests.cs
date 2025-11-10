using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Contrib.HttpClient;
using NUnit.Framework;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Services;

namespace TeslaMateAgile.Tests.Services;

public class PGEServiceTests
{
    private PGEService _subject;
    private Mock<HttpMessageHandler> _handler;

    [SetUp]
    public void Setup()
    {
        _handler = new Mock<HttpMessageHandler>();
        var httpClient = _handler.CreateClient();
        var pgeOptions = Options.Create(new PGEOptions 
        { 
            BaseUrl = "https://pge-pe-api.gridx.com",
            Utility = "PGE",
            Market = "DAM",
            RateName = "EV2A",
            RepresentativeCircuitId = "083611114",
            Program = "CalFUSE"
        });
        httpClient.BaseAddress = new Uri(pgeOptions.Value.BaseUrl);
        
        var mockLogger = new Mock<ILogger<PGEService>>();
        _subject = new PGEService(httpClient, pgeOptions, mockLogger.Object);
    }

    [Test]
    public async Task TestAsync()
    {
        var jsonFile = "pge_test.json";
        var json = File.ReadAllText(Path.Combine("Prices", jsonFile));

        _handler.SetupAnyRequest()
            .ReturnsResponse(json, "application/json");

        var startDate = DateTimeOffset.Parse("2023-10-26T00:00:00-07:00");
        var endDate = DateTimeOffset.Parse("2023-10-26T05:00:00-07:00");
        var prices = await _subject.GetPriceData(startDate, endDate);
        var priceList = prices.ToList();

        // The service now requests multiple days (with buffer days for timezone handling)
        // so we expect 3 requests: one day before, the actual day, and one day after
        _handler.VerifyAnyRequest(Times.Exactly(3));

        // The service should filter and return only prices that overlap with the requested range
        // Since we're fetching 3 days of data (each with 5 intervals in the test data)
        // but only filtering to the requested 5-hour period, we should get 5 intervals
        Assert.That(priceList.Count, Is.GreaterThanOrEqualTo(5), "Should return at least the 5 intervals in the requested range");
        
        // Verify the first and last prices are within or overlap the requested range
        var firstPrice = priceList.First();
        var lastPrice = priceList.Last();
        
        Assert.That(firstPrice.ValidFrom, Is.LessThanOrEqualTo(endDate), "First price should start before or at end date");
        Assert.That(lastPrice.ValidTo, Is.GreaterThanOrEqualTo(startDate), "Last price should end after or at start date");
        
        // Find the price intervals that exactly match our test data expectations
        var exactMatches = priceList.Where(p => 
            p.ValidFrom >= startDate && p.ValidTo <= endDate).ToList();
        
        Assert.That(exactMatches.Count, Is.EqualTo(5), "Should have 5 complete intervals within the range");
        Assert.That(exactMatches[0].ValidFrom, Is.EqualTo(DateTimeOffset.Parse("2023-10-26T00:00:00-07:00")));
        Assert.That(exactMatches[0].ValidTo, Is.EqualTo(DateTimeOffset.Parse("2023-10-26T01:00:00-07:00")));
        Assert.That(exactMatches[0].Value, Is.EqualTo(0.15234M));
        Assert.That(exactMatches[4].ValidFrom, Is.EqualTo(DateTimeOffset.Parse("2023-10-26T04:00:00-07:00")));
        Assert.That(exactMatches[4].ValidTo, Is.EqualTo(DateTimeOffset.Parse("2023-10-26T05:00:00-07:00")));
        Assert.That(exactMatches[4].Value, Is.EqualTo(0.14456M));
    }

    [Test]
    public void Constructor_ShouldThrowException_WhenRateNameIsEmpty()
    {
        var httpClient = new HttpClient();
        var pgeOptions = Options.Create(new PGEOptions 
        { 
            BaseUrl = "https://pge-pe-api.gridx.com",
            Utility = "PGE",
            Market = "DAM",
            RateName = "", // Empty RateName
            RepresentativeCircuitId = "083611114",
            Program = "CalFUSE"
        });
        var mockLogger = new Mock<ILogger<PGEService>>();
        
        var ex = Assert.Throws<InvalidOperationException>(() => 
            new PGEService(httpClient, pgeOptions, mockLogger.Object));
        
        Assert.That(ex.Message, Does.Contain("PGE RateName is required"));
    }

    [Test]
    public void Constructor_ShouldThrowException_WhenRepresentativeCircuitIdIsEmpty()
    {
        var httpClient = new HttpClient();
        var pgeOptions = Options.Create(new PGEOptions 
        { 
            BaseUrl = "https://pge-pe-api.gridx.com",
            Utility = "PGE",
            Market = "DAM",
            RateName = "EV2A",
            RepresentativeCircuitId = "", // Empty RepresentativeCircuitId
            Program = "CalFUSE"
        });
        var mockLogger = new Mock<ILogger<PGEService>>();
        
        var ex = Assert.Throws<InvalidOperationException>(() => 
            new PGEService(httpClient, pgeOptions, mockLogger.Object));
        
        Assert.That(ex.Message, Does.Contain("PGE RepresentativeCircuitId is required"));
    }
}
