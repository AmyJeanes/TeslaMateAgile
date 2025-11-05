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
        _subject = new PGEService(httpClient, pgeOptions);
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

        _handler.VerifyAnyRequest(Times.Once());

        Assert.That(priceList.Count, Is.EqualTo(5));
        Assert.That(priceList[0].ValidFrom, Is.EqualTo(DateTimeOffset.Parse("2023-10-26T00:00:00-07:00")));
        Assert.That(priceList[0].ValidTo, Is.EqualTo(DateTimeOffset.Parse("2023-10-26T01:00:00-07:00")));
        Assert.That(priceList[0].Value, Is.EqualTo(0.15234M));
        Assert.That(priceList[4].ValidFrom, Is.EqualTo(DateTimeOffset.Parse("2023-10-26T04:00:00-07:00")));
        Assert.That(priceList[4].ValidTo, Is.EqualTo(DateTimeOffset.Parse("2023-10-26T05:00:00-07:00")));
        Assert.That(priceList[4].Value, Is.EqualTo(0.14456M));
    }
}
