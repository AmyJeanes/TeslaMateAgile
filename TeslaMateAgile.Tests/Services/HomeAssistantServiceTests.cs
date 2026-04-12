using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Contrib.HttpClient;
using NUnit.Framework;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Helpers.Interfaces;
using TeslaMateAgile.Services;

namespace TeslaMateAgile.Tests.Services;

public class HomeAssistantServiceTests
{
    private HomeAssistantService _subject;
    private Mock<HttpMessageHandler> _handler;
    private Mock<IRateLimitHelper> _rateLimitHelper;

    [SetUp]
    public void Setup()
    {
        _handler = new Mock<HttpMessageHandler>();
        var httpClient = _handler.CreateClient();
        var homeAssistantOptions = Options.Create(new HomeAssistantOptions { BaseUrl = "http://homeassistant", EntityId = "input_number.test" });
        httpClient.BaseAddress = new Uri(homeAssistantOptions.Value.BaseUrl);
        _rateLimitHelper = new Mock<IRateLimitHelper>();
        var teslaMateOptions = Options.Create(new TeslaMateOptions { });
        var mockLogger = new Mock<ILogger<HomeAssistantService>>();
        _subject = new HomeAssistantService(httpClient, _rateLimitHelper.Object, homeAssistantOptions, teslaMateOptions, mockLogger.Object);
    }

    [Test]
    public async Task TestAsync()
    {
        var jsonFile = "ha_test.json";
        var json = File.ReadAllText(Path.Combine("Prices", jsonFile));

        _handler.SetupAnyRequest()
            .ReturnsResponse(json, "application/json");

        var startDate = DateTimeOffset.Parse("2023-08-24T23:43:53Z");
        var endDate = DateTimeOffset.Parse("2023-08-25T03:19:42Z");
        var priceData = await _subject.GetPriceData(startDate, endDate);
        var priceList = priceData.Prices.ToList();

        _handler.VerifyAnyRequest(Times.Once());
        _rateLimitHelper.Verify(x => x.AddRequest(), Times.Once);

        Assert.That(priceList.Count, Is.EqualTo(1));
        Assert.That(priceList[0].ValidFrom, Is.EqualTo(startDate));
        Assert.That(priceList[0].ValidTo, Is.EqualTo(endDate));
        Assert.That(priceList[0].Value, Is.EqualTo(0.2748M));
    }

    [Test]
    public async Task Filters_Non_Numeric_States()
    {
        var json = File.ReadAllText(Path.Combine("Prices", "ha_test_non_numeric_states.json"));

        _handler.SetupAnyRequest()
            .ReturnsResponse(json, "application/json");

        var startDate = DateTimeOffset.Parse("2023-08-24T23:43:53Z");
        var endDate = DateTimeOffset.Parse("2023-08-25T03:19:42Z");
        var priceData = await _subject.GetPriceData(startDate, endDate);
        var priceList = priceData.Prices.ToList();

        // 6 entries in the JSON, 2 are non-numeric (unavailable, unknown) → 4 prices
        Assert.That(priceList.Count, Is.EqualTo(4));
        Assert.That(priceList.All(p => p.Value > 0), Is.True, "All prices should be numeric and positive");

        // Verify the unavailable/unknown entries were skipped and time ranges are correct
        // The price before 'unavailable' should extend to the next valid entry's timestamp
        Assert.That(priceList[0].Value, Is.EqualTo(0.0095M));
        Assert.That(priceList[1].Value, Is.EqualTo(0.0096M));
        Assert.That(priceList[1].ValidTo, Is.EqualTo(priceList[2].ValidFrom),
            "Price interval should bridge over the filtered unavailable entry");
        Assert.That(priceList[2].Value, Is.EqualTo(0.0098M));
        Assert.That(priceList[3].Value, Is.EqualTo(0.0100M));
        Assert.That(priceList[3].ValidTo, Is.EqualTo(endDate));
    }

    [Test]
    public async Task Extends_First_Price_When_Leading_Entries_Filtered()
    {
        var json = File.ReadAllText(Path.Combine("Prices", "ha_test_leading_unavailable.json"));

        _handler.SetupAnyRequest()
            .ReturnsResponse(json, "application/json");

        var startDate = DateTimeOffset.Parse("2023-08-24T23:43:53Z");
        var endDate = DateTimeOffset.Parse("2023-08-25T03:19:42Z");
        var priceData = await _subject.GetPriceData(startDate, endDate);
        var priceList = priceData.Prices.ToList();

        // First entry is unavailable, so the first valid price should extend back to cover startDate
        Assert.That(priceList.Count, Is.EqualTo(2));
        Assert.That(priceList[0].ValidFrom, Is.EqualTo(startDate),
            "First price should extend back to the requested start date");
        Assert.That(priceList[0].Value, Is.EqualTo(0.0095M));
        Assert.That(priceList[1].Value, Is.EqualTo(0.0100M));
        Assert.That(priceList[1].ValidTo, Is.EqualTo(endDate));
    }
}
