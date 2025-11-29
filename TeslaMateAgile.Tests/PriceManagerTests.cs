using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using NUnit.Framework;
using TeslaMateAgile.Data;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Data.TeslaMate;
using TeslaMateAgile.Data.TeslaMate.Entities;
using TeslaMateAgile.Helpers.Interfaces;
using TeslaMateAgile.Managers;
using TeslaMateAgile.Services.Interfaces;

namespace TeslaMateAgile.Tests;

public class PriceManagerTests
{
    private AutoMocker _mocker;
    private PriceManager _subject;

    [SetUp]
    public void Setup()
    {
        _mocker = new AutoMocker();

        var teslaMateDbContext = new Mock<TeslaMateDbContext>(new DbContextOptions<TeslaMateDbContext>());
        _mocker.Use(teslaMateDbContext);

        var logger = new ServiceCollection()
            .AddLogging(x => x.AddConsole().SetMinimumLevel(LogLevel.Debug))
            .BuildServiceProvider()
            .GetRequiredService<ILogger<PriceManager>>();
        _mocker.Use(logger);

        var teslaMateOptions = Options.Create(new TeslaMateOptions()
        {
            MatchingStartToleranceMinutes = 30,
            MatchingEndToleranceMinutes = 120,
            MatchingEnergyToleranceRatio = 0.1M
        });
        _mocker.Use(teslaMateOptions);
    }

    private static readonly object[][] PriceManager_CalculateChargeCost_Cases = new object[][] {
            new object[]
            {
                "Plunge",
                TestHelpers.ImportAgilePrices("plunge_test.json"),
                TestHelpers.ImportCharges("plunge_test.csv"),
                -2.00M,
                36.74M,
            },
            new object[]
            {
                "DaylightSavingsTime",
                new List<Price>
                {
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2021-04-13T20:30:00+01:00"),
                        ValidTo = DateTimeOffset.Parse("2021-04-13T23:30:00+01:00"),
                        Value = 4.5M
                    }
                },
                TestHelpers.ImportCharges("daylightsavingstime_test.csv"),
                75.5M,
                16.78M,
            },
            new object[]
            {
                "ExactMillisecond",
                new List<Price>
                {
                    new Price
                    {
                        ValidFrom = DateTimeOffset.Parse("2023-08-24T23:43:53.026Z"),
                        ValidTo =   DateTimeOffset.Parse("2023-08-25T03:19:42.588Z"),
                        Value = 0.2748M
                    }
                },
                TestHelpers.ImportCharges("exactmillisecond_test.csv"),
                5.88M,
                21.41M,
            }
        };

    [Test]
    [TestCaseSource(nameof(PriceManager_CalculateChargeCost_Cases))]
    public async Task PriceManager_CalculateChargeCost(string testName, List<Price> prices, List<Charge> charges, decimal expectedPrice, decimal expectedEnergy)
    {
        Console.WriteLine($"Running calculate charge cost test '{testName}'");
        SetupDynamicPriceDataService(prices);
        _subject = _mocker.CreateInstance<PriceManager>();
        var (price, energy) = await _subject.CalculateChargeCost(charges);
        Assert.That(expectedPrice, Is.EqualTo(price));
        Assert.That(expectedEnergy, Is.EqualTo(energy));
    }

    private static readonly object[][] PriceManager_CalculateEnergyUsed_Cases = new object[][] {
            new object[]
            {
                "ThreePhase",
                TestHelpers.ImportCharges("threephase_test.csv"),
                47.65M,
            }
        };

    [Test]
    [TestCaseSource(nameof(PriceManager_CalculateEnergyUsed_Cases))]
    public void PriceManager_CalculateEnergyUsed(string testName, List<Charge> charges, decimal expectedEnergy)
    {
        Console.WriteLine($"Running calculate energy used test '{testName}'");
        SetupDynamicPriceDataService();
        _subject = _mocker.CreateInstance<PriceManager>();
        var phases = _subject.DeterminePhases(charges);
        if (!phases.HasValue) { throw new Exception("Phases has no value"); }
        var energy = _subject.CalculateEnergyUsed(charges, phases.Value);
        Assert.That(expectedEnergy, Is.EqualTo(Math.Round(energy, 2)));
    }

    [Test]
    public async Task PriceManager_NoPhaseData()
    {
        var charges = TestHelpers.ImportCharges("nophasedata_test.csv");
        SetupDynamicPriceDataService();
        _subject = _mocker.CreateInstance<PriceManager>();
        var (price, energy) = await _subject.CalculateChargeCost(charges);
        Assert.That(0, Is.EqualTo(price));
        Assert.That(0, Is.EqualTo(energy));
    }

    private static readonly object[][] PriceManager_CalculateWholeChargeCost_Cases = new object[][] {
            new object[]
            {
                "WholeCharge",
                new List<ProviderCharge>
                {
                    new ProviderCharge
                    {
                        Cost = 10.00M,
                        StartTime = DateTimeOffset.Parse("2023-08-24T23:30:00Z"),
                        EndTime = DateTimeOffset.Parse("2023-08-25T03:00:00Z")
                    }
                },
                TestHelpers.ImportCharges("exactmillisecond_test.csv"),
                10.00M,
                21.41M,
            },
            new object[]
            {
                "InvalidPhasesDefaultZero",
                new List<ProviderCharge>
                {
                    new ProviderCharge
                    {
                        Cost = 2.38M,
                        StartTime = DateTimeOffset.Parse("2024-04-07T23:00:54Z"),
                        EndTime = DateTimeOffset.Parse("2024-04-07T05:59:15Z"),
                        EnergyKwh = 10M
                    },
                    new ProviderCharge
                    {
                        Cost = 2.38M,
                        StartTime = DateTimeOffset.Parse("2024-04-07T21:15:44Z"),
                        EndTime = DateTimeOffset.Parse("2024-04-07T21:15:44Z"),
                        EnergyKwh = 10M
                    },
                    new ProviderCharge
                    {
                        Cost = 2.38M,
                        StartTime = DateTimeOffset.Parse("2024-04-07T13:26:25Z"),
                        EndTime = DateTimeOffset.Parse("2024-04-07T14:36:59Z"),
                        EnergyKwh = 10M
                    }
                },
                TestHelpers.ImportCharges("invalidphases.csv"),
                0M,
                0M,
            }
        };

    [Test]
    [TestCaseSource(nameof(PriceManager_CalculateWholeChargeCost_Cases))]
    public async Task PriceManager_CalculateWholeChargeCost(string testName, List<ProviderCharge> providerCharges, List<Charge> charges, decimal expectedPrice, decimal expectedEnergy)
    {
        Console.WriteLine($"Running calculate whole charge cost test '{testName}'");
        SetupWholePriceDataService(providerCharges);
        _subject = _mocker.CreateInstance<PriceManager>();
        var (price, energy) = await _subject.CalculateChargeCost(charges);
        Assert.That(expectedPrice, Is.EqualTo(price));
        Assert.That(expectedEnergy, Is.EqualTo(energy));
    }

    private static readonly object[][] PriceManager_LocateMostAppropriateCharge_Cases = new object[][] {
            new object[]
            {
                "WithoutEnergy",
                new List<ProviderCharge>
                {
                    new ProviderCharge
                    {
                        Cost = 10.00M,
                        StartTime = DateTimeOffset.Parse("2023-08-24T23:30:00Z"),
                        EndTime = DateTimeOffset.Parse("2023-08-25T03:00:00Z")
                    },
                    new ProviderCharge
                    {
                        Cost = 15.00M,
                        StartTime = DateTimeOffset.Parse("2023-08-24T23:00:00Z"),
                        EndTime = DateTimeOffset.Parse("2023-08-25T03:30:00Z")
                    },
                    new ProviderCharge
                    {
                        Cost = 20.00M,
                        StartTime = DateTimeOffset.Parse("2023-08-24T22:30:00Z"),
                        EndTime = DateTimeOffset.Parse("2023-08-25T04:00:00Z")
                    }
                },
                DateTimeOffset.Parse("2023-08-24T23:30:00Z"),
                DateTimeOffset.Parse("2023-08-25T03:00:00Z"),
                30M,
                10.00M
            },
            new object[]
            {
                "WithEnergy",
                new List<ProviderCharge>
                {
                    new ProviderCharge
                    {
                        Cost = 10.00M,
                        EnergyKwh = 25M,
                        StartTime = DateTimeOffset.Parse("2023-08-24T23:30:00Z"),
                        EndTime = DateTimeOffset.Parse("2023-08-25T03:00:00Z")
                    },
                    new ProviderCharge
                    {
                        Cost = 15.00M,
                        EnergyKwh = 31M,
                        StartTime = DateTimeOffset.Parse("2023-08-24T23:05:00Z"),
                        EndTime = DateTimeOffset.Parse("2023-08-25T03:35:00Z")
                    },
                    new ProviderCharge
                    {
                        Cost = 20.00M,
                        EnergyKwh = 25M,
                        StartTime = DateTimeOffset.Parse("2023-08-24T23:00:00Z"),
                        EndTime = DateTimeOffset.Parse("2023-08-25T03:30:00Z")
                    },
                    new ProviderCharge
                    {
                        Cost = 25.00M,
                        EnergyKwh = 25M,
                        StartTime = DateTimeOffset.Parse("2023-08-24T22:30:00Z"),
                        EndTime = DateTimeOffset.Parse("2023-08-25T04:00:00Z")
                    }
                },
                DateTimeOffset.Parse("2023-08-24T23:30:00Z"),
                DateTimeOffset.Parse("2023-08-25T03:00:00Z"),
                30M,
                15.00M
            },
            new object[]
            {
                "WithMixedEnergy",
                new List<ProviderCharge>
                {
                    new ProviderCharge
                    {
                        Cost = 9.00M,
                        StartTime = DateTimeOffset.Parse("2023-08-24T23:20:00Z"),
                        EndTime = DateTimeOffset.Parse("2023-08-25T02:50:00Z")
                    },
                    new ProviderCharge
                    {
                        Cost = 12.50M,
                        EnergyKwh = 29.5M,
                        StartTime = DateTimeOffset.Parse("2023-08-24T23:28:00Z"),
                        EndTime = DateTimeOffset.Parse("2023-08-25T03:05:00Z")
                    },
                    new ProviderCharge
                    {
                        Cost = 18.00M,
                        StartTime = DateTimeOffset.Parse("2023-08-24T23:10:00Z"),
                        EndTime = DateTimeOffset.Parse("2023-08-25T03:20:00Z")
                    }
                },
                DateTimeOffset.Parse("2023-08-24T23:30:00Z"),
                DateTimeOffset.Parse("2023-08-25T03:00:00Z"),
                30M,
                12.50M
            },
            new object[]
            {
                "ZeroEnergyWithAvailableData",
                new List<ProviderCharge>
                {
                    new ProviderCharge
                    {
                        Cost = 11.00M,
                        EnergyKwh = 25M,
                        StartTime = DateTimeOffset.Parse("2023-08-24T23:32:00Z"),
                        EndTime = DateTimeOffset.Parse("2023-08-25T03:02:00Z")
                    },
                    new ProviderCharge
                    {
                        Cost = 13.00M,
                        StartTime = DateTimeOffset.Parse("2023-08-24T23:29:00Z"),
                        EndTime = DateTimeOffset.Parse("2023-08-25T03:01:00Z")
                    }
                },
                DateTimeOffset.Parse("2023-08-24T23:30:00Z"),
                DateTimeOffset.Parse("2023-08-25T03:00:00Z"),
                0M,
                13.00M
            }
        };

    [Test]
    [TestCaseSource(nameof(PriceManager_LocateMostAppropriateCharge_Cases))]
    public void PriceManager_LocateMostAppropriateCharge(string testName, List<ProviderCharge> providerCharges, DateTimeOffset minDate, DateTimeOffset maxDate, decimal energyUsed, decimal expectedCost)
    {
        Console.WriteLine($"Running locate most appropriate charge test '{testName}'");
        SetupWholePriceDataService(providerCharges);
        _subject = _mocker.CreateInstance<PriceManager>();
        var mostAppropriateCharge = _subject.LocateMostAppropriateCharge(providerCharges, energyUsed, minDate, maxDate);
        Assert.That(expectedCost, Is.EqualTo(mostAppropriateCharge.Cost));
    }

    [Test]
    public async Task PriceManager_Update_StopsWhenRateLimitAlreadyReached()
    {
        using var context = CreateInMemoryContext();
        context.Geofences.Add(new Geofence { Id = 1, Name = "Home" });
        var charge = new Charge
        {
            Id = 1,
            ChargeEnergyAdded = 1,
            ChargerPower = 1,
#pragma warning disable CS0618
            DateInternal = DateTime.UtcNow.AddHours(-2)
#pragma warning restore CS0618
        };
        var processEntity = new ChargingProcess
        {
            Id = 1,
            GeofenceId = 1,
            StartDate = DateTime.UtcNow.AddHours(-2),
            EndDate = DateTime.UtcNow.AddHours(-1),
            Charges = new List<Charge> { charge }
        };
        charge.ChargingProcess = processEntity;
        context.ChargingProcesses.Add(processEntity);
        context.SaveChanges();

        var mockRateLimitHelper = new Mock<IRateLimitHelper>();
        mockRateLimitHelper.Setup(x => x.HasReachedRateLimit()).Returns(true);
        mockRateLimitHelper.Setup(x => x.GetNextReset()).Returns(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));

        var mockLogger = new Mock<ILogger<PriceManager>>();

        var options = Options.Create(new TeslaMateOptions
        {
            GeofenceId = 1,
            MatchingStartToleranceMinutes = 30,
            MatchingEndToleranceMinutes = 120,
            MatchingEnergyToleranceRatio = 0.1M,
            RateLimitMaxRequests = 2,
            RateLimitPeriodSeconds = 60
        });

        SetupDynamicPriceDataService();
        _mocker.Use(context);
        _mocker.Use(mockRateLimitHelper.Object);
        _mocker.Use(mockLogger.Object);
        _mocker.Use(options);

        _subject = _mocker.CreateInstance<PriceManager>();

        await _subject.Update();

        mockRateLimitHelper.Verify(x => x.HasReachedRateLimit(), Times.Once);
        mockRateLimitHelper.Verify(x => x.GetNextReset(), Times.Once);

        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Rate limit reached, stopping price calculations for this run")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);

        var process = context.ChargingProcesses.Single();
        Assert.That(process.Cost, Is.Null);
    }

    [Test]
    public async Task PriceManager_Update_HandlesRateLimitExceptionDuringProcessing()
    {
        using var context = CreateInMemoryContext();
        context.Geofences.Add(new Geofence { Id = 1, Name = "Home" });
        var charge = new Charge
        {
            Id = 1,
            ChargeEnergyAdded = 1,
            ChargerPower = 1,
#pragma warning disable CS0618
            DateInternal = DateTime.UtcNow.AddHours(-2)
#pragma warning restore CS0618
        };
        var processEntity = new ChargingProcess
        {
            Id = 1,
            GeofenceId = 1,
            StartDate = DateTime.UtcNow.AddHours(-2),
            EndDate = DateTime.UtcNow.AddHours(-1),
            Charges = new List<Charge> { charge }
        };
        charge.ChargingProcess = processEntity;
        context.ChargingProcesses.Add(processEntity);
        context.SaveChanges();

        var resetTime = DateTimeOffset.Parse("2024-01-01T01:00:00Z");
        var mockRateLimitHelper = new Mock<IRateLimitHelper>();
        mockRateLimitHelper.SetupSequence(x => x.HasReachedRateLimit())
            .Returns(false)
            .Returns(false);
        mockRateLimitHelper.Setup(x => x.GetNextReset()).Returns(resetTime);

        var mockLogger = new Mock<ILogger<PriceManager>>();

        var priceDataService = new Mock<IPriceDataService>();
        priceDataService
            .As<IDynamicPriceDataService>()
            .Setup(x => x.GetPriceData(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .ThrowsAsync(new RateLimitException());

        var options = Options.Create(new TeslaMateOptions
        {
            GeofenceId = 1,
            MatchingStartToleranceMinutes = 30,
            MatchingEndToleranceMinutes = 120,
            MatchingEnergyToleranceRatio = 0.1M,
            RateLimitMaxRequests = 2,
            RateLimitPeriodSeconds = 60
        });

        _mocker.Use(context);
        _mocker.Use(mockRateLimitHelper.Object);
        _mocker.Use(mockLogger.Object);
        _mocker.Use(priceDataService.Object);
        _mocker.Use(options);

        _subject = _mocker.CreateInstance<PriceManager>();

        await _subject.Update();

        mockRateLimitHelper.Verify(x => x.GetNextReset(), Times.Once);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Rate limit reached during price calculation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);

        var process = context.ChargingProcesses.Single();
        Assert.That(process.Cost, Is.Null);
    }

    private static TeslaMateDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<TeslaMateDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TeslaMateDbContext(options);
    }

    private void SetupDynamicPriceDataService(List<Price> prices = null)
    {
        if (prices == null) { prices = new List<Price>(); }

        var priceDataService = new Mock<IPriceDataService>();

        priceDataService
            .As<IDynamicPriceDataService>()
            .Setup(x => x.GetPriceData(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(new PriceData(prices.OrderBy(x => x.ValidFrom)));

        _mocker.Use(priceDataService.Object);
    }

    private void SetupWholePriceDataService(List<ProviderCharge> providerCharges = null)
    {
        if (providerCharges == null) { providerCharges = new List<ProviderCharge>(); }

        var priceDataService = new Mock<IPriceDataService>();

        priceDataService
            .As<IWholePriceDataService>()
            .Setup(x => x.GetCharges(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(new ProviderChargeData(providerCharges));

        _mocker.Use(priceDataService.Object);
    }
}
