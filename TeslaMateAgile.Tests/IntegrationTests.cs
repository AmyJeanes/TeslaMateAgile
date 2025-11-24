using GraphQL.Client.Abstractions;
using GraphQL.Client.Serializer.SystemTextJson;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Helpers.Interfaces;
using TeslaMateAgile.Managers;
using TeslaMateAgile.Services;
using TeslaMateAgile.Services.Interfaces;

namespace TeslaMateAgile.Tests;

public class IntegrationTests
{
    private const string IntegrationTest = "Integration test";

    [Ignore(IntegrationTest)]
    [Test]
    public async Task IntegrationTests_Tibber()
    {
        var configBuilder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddUserSecrets<Program>();

        var config = configBuilder.Build();

        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddTransient<IGraphQLJsonSerializer, SystemTextJsonSerializer>();
        services.Configure<TibberOptions>(config.GetSection("Tibber"));
        services.AddHttpClient<IDynamicPriceDataService, TibberService>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<TibberOptions>>().Value;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.AccessToken);
        });

        var priceDataService = services.BuildServiceProvider().GetRequiredService<IDynamicPriceDataService>();

        var from = DateTimeOffset.Parse("2020-01-01T00:25:00+00:00");
        var to = DateTimeOffset.Parse("2020-01-01T15:00:00+00:00");

        var priceData = await priceDataService.GetPriceData(from, to);

        Assert.That(priceData.Prices.Min(x => x.ValidFrom), Is.LessThanOrEqualTo(from));
        Assert.That(priceData.Prices.Max(x => x.ValidTo), Is.GreaterThanOrEqualTo(to));
    }

    [Ignore(IntegrationTest)]
    [Test]
    public async Task IntegrationTests_Awattar()
    {
        var configBuilder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddUserSecrets<Program>();

        var config = configBuilder.Build();

        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddOptions<AwattarOptions>()
                        .Bind(config.GetSection("Awattar"))
                        .ValidateDataAnnotations();
        services.AddHttpClient<IDynamicPriceDataService, AwattarService>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<AwattarOptions>>().Value;
            var baseUrl = options.BaseUrl;
            if (!baseUrl.EndsWith("/")) { baseUrl += "/"; }
            client.BaseAddress = new Uri(baseUrl);
        });

        var priceDataService = services.BuildServiceProvider().GetRequiredService<IDynamicPriceDataService>();

        var from = DateTimeOffset.Parse("2020-01-01T00:25:00+00:00");
        var to = DateTimeOffset.Parse("2020-01-01T15:55:00+00:00");

        var priceData = await priceDataService.GetPriceData(from, to);

        Assert.That(priceData.Prices.Min(x => x.ValidFrom), Is.LessThanOrEqualTo(from));
        Assert.That(priceData.Prices.Max(x => x.ValidTo), Is.GreaterThanOrEqualTo(to));
    }

    [Ignore(IntegrationTest)]
    [Test]
    public async Task IntegrationTests_Energinet()
    {
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Energinet:BaseUrl"] = "https://api.energidataservice.dk/dataset/",
                ["Energinet:Region"] = "DK1",
                ["Energinet:Currency"] = "DKK",
                ["Energinet:VAT"] = "1.25",
                ["Energinet:ClampNegativePrices"] = "true",
                ["Energinet:FixedPrices:TimeZone"] = "Europe/London",
                ["Energinet:FixedPrices:Prices:0"] = "00:00-12:00=0.25",
                ["Energinet:FixedPrices:Prices:1"] = "12:00-00:00=0.50"
            });

        var config = configBuilder.Build();

        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddOptions<EnerginetOptions>()
                        .Bind(config.GetSection("Energinet"))
                        .ValidateDataAnnotations();
        services.AddHttpClient<IDynamicPriceDataService, EnerginetService>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<EnerginetOptions>>().Value;
            var baseUrl = options.BaseUrl;
            if (!baseUrl.EndsWith("/")) { baseUrl += "/"; }
            client.BaseAddress = new Uri(baseUrl);
        });
        var priceDataService = services.BuildServiceProvider().GetRequiredService<IDynamicPriceDataService>();

        var from = new DateTimeOffset(2025, 10, 2, 0, 0, 0, new TimeSpan(1, 0, 0));
        var to = new DateTimeOffset(2025, 10, 2, 23, 59, 0, new TimeSpan(1, 0, 0));

        var priceData = await priceDataService.GetPriceData(from, to);

        foreach (var price in priceData.Prices)
        {
            Console.WriteLine($"{price.ValidFrom} - {price.ValidTo} - {price.Value}");
        }

        Assert.That(priceData.Prices.Min(x => x.ValidFrom), Is.LessThanOrEqualTo(from));
        Assert.That(priceData.Prices.Max(x => x.ValidTo), Is.GreaterThanOrEqualTo(to));
    }

    [Ignore(IntegrationTest)]
    [Test]
    public async Task IntegrationTests_Monta()
    {
        var configBuilder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddUserSecrets<Program>();

        var config = configBuilder.Build();

        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddOptions<MontaOptions>()
                        .Bind(config.GetSection("Monta"))
                        .ValidateDataAnnotations();
        services.AddHttpClient<IWholePriceDataService, MontaService>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<MontaOptions>>().Value;
            var baseUrl = options.BaseUrl;
            if (!baseUrl.EndsWith("/")) { baseUrl += "/"; }
            client.BaseAddress = new Uri(baseUrl);
        });

        var logger = new ServiceCollection()
            .AddLogging(x => x.AddConsole().SetMinimumLevel(LogLevel.Debug))
            .BuildServiceProvider()
            .GetRequiredService<ILogger<PriceManager>>();

        var priceDataService = services.BuildServiceProvider().GetRequiredService<IWholePriceDataService>();

        var rateLimitHelper = services.BuildServiceProvider().GetRequiredService<IRateLimitHelper>();

        var options = new TeslaMateOptions
        {
            MatchingStartToleranceMinutes = 120,
            MatchingEndToleranceMinutes = 240,
            MatchingEnergyToleranceRatio = 0.2M
        };
        var priceManager = new PriceManager(logger, null, priceDataService, rateLimitHelper, Options.Create(options));

        var from = DateTimeOffset.Parse("2024-10-17T05:51:55+00:00");
        var to = DateTimeOffset.Parse("2024-10-17T08:57:54+00:00");
        var energyUsed = 30M;

        var providerChargeData = await priceDataService.GetCharges(from.AddMinutes(-120), to.AddMinutes(120));
        var possibleCharges = providerChargeData.Charges;

        var appropriateCharge = priceManager.LocateMostAppropriateCharge(possibleCharges, energyUsed, from, to);

        Assert.That(possibleCharges, Is.Not.Empty);
    }
}
