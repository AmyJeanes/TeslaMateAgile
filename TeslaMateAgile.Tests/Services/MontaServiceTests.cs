using Microsoft.Extensions.Options;
using Moq;
using Moq.Contrib.HttpClient;
using NUnit.Framework;
using System.Text.Json;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Helpers.Interfaces;
using TeslaMateAgile.Services;
using TeslaMateAgile.Services.Interfaces;

namespace TeslaMateAgile.Tests.Services
{
    public class MontaServiceTests
    {
        private MontaService _subject;
        private Mock<HttpMessageHandler> _handler;
        private Mock<IRateLimitHelper> _rateLimitHelper;

        [SetUp]
        public void Setup()
        {
            _handler = new Mock<HttpMessageHandler>();
            var httpClient = _handler.CreateClient();
            var montaOptions = Options.Create(new MontaOptions
            {
                BaseUrl = "https://public-api.monta.com/api/v1",
                ClientId = "test-client-id",
                ClientSecret = "test-client-secret",
                ChargePointId = 123
            });
            httpClient.BaseAddress = new Uri(montaOptions.Value.BaseUrl);
            _rateLimitHelper = new Mock<IRateLimitHelper>();
            _rateLimitHelper.Setup(x => x.Configure(It.IsAny<IRateLimitedService>())).Returns(_rateLimitHelper.Object);
            _subject = new MontaService(httpClient, _rateLimitHelper.Object, montaOptions);
        }

        [Test]
        public async Task GetCharges_ShouldIncludeChargePointIdQueryParameter_WhenSetInMontaOptions()
        {
            var from = DateTimeOffset.Parse("2024-10-17T00:00:00+00:00");
            var to = DateTimeOffset.Parse("2024-10-17T15:00:00+00:00");

            var accessTokenResponse = new
            {
                accessToken = "test-access-token"
            };
            var chargesResponse = new
            {
                data = new[]
                {
                    new
                    {
                        startedAt = from,
                        stoppedAt = to,
                        cost = 10.0M,
                        consumedKwh = 15.0M
                    }
                }
            };

            _handler.SetupRequest(HttpMethod.Post, "https://public-api.monta.com/api/v1/auth/token")
                .ReturnsResponse(JsonSerializer.Serialize(accessTokenResponse), "application/json");

            var fromDate = from.AddHours(MontaService.FetchHoursBeforeFrom).UtcDateTime;
            var toDate = to.AddHours(MontaService.FetchHoursAfterTo).UtcDateTime;
            _handler.SetupRequest(HttpMethod.Get, $"https://public-api.monta.com/api/v1/charges?state=completed&fromDate={fromDate:o}&toDate={toDate:o}&chargePointId=123")
                .ReturnsResponse(JsonSerializer.Serialize(chargesResponse), "application/json");

            var providerChargeData = await _subject.GetCharges(from, to);
            var charges = providerChargeData.Charges;

            Assert.That(charges, Is.Not.Empty);
            Assert.That(charges.First().Cost, Is.EqualTo(10.0M));
            Assert.That(charges.First().EnergyKwh, Is.EqualTo(15.0M));
            Assert.That(charges.First().StartTime, Is.EqualTo(from));
            Assert.That(charges.First().EndTime, Is.EqualTo(to));
            _rateLimitHelper.Verify(x => x.AddRequest(), Times.Exactly(2));
        }

        [Test]
        public async Task GetCharges_ShouldNotIncludeChargePointIdQueryParameter_WhenNotSetInMontaOptions()
        {
            var from = DateTimeOffset.Parse("2024-10-17T00:00:00+00:00");
            var to = DateTimeOffset.Parse("2024-10-17T15:00:00+00:00");

            var montaOptions = Options.Create(new MontaOptions
            {
                BaseUrl = "https://public-api.monta.com/api/v1",
                ClientId = "test-client-id",
                ClientSecret = "test-client-secret"
            });

            var rateLimitHelperMock = new Mock<IRateLimitHelper>();
            rateLimitHelperMock.Setup(x => x.Configure(It.IsAny<IRateLimitedService>())).Returns(rateLimitHelperMock.Object);

            _subject = new MontaService(_handler.CreateClient(), rateLimitHelperMock.Object, montaOptions);

            var accessTokenResponse = new
            {
                accessToken = "test-access-token"
            };
            var chargesResponse = new
            {
                data = new[]
                {
                    new
                    {
                        startedAt = from,
                        stoppedAt = to,
                        cost = 10.0M,
                        consumedKwh = 15.0M
                    }
                }
            };

            _handler.SetupRequest(HttpMethod.Post, "https://public-api.monta.com/api/v1/auth/token")
                .ReturnsResponse(JsonSerializer.Serialize(accessTokenResponse), "application/json");

            var fromDate = from.AddHours(MontaService.FetchHoursBeforeFrom).UtcDateTime;
            var toDate = to.AddHours(MontaService.FetchHoursAfterTo).UtcDateTime;
            _handler.SetupRequest(HttpMethod.Get, $"https://public-api.monta.com/api/v1/charges?state=completed&fromDate={fromDate:o}&toDate={toDate:o}")
                .ReturnsResponse(JsonSerializer.Serialize(chargesResponse), "application/json");

            var providerChargeData = await _subject.GetCharges(from, to);
            var charges = providerChargeData.Charges;

            Assert.That(charges, Is.Not.Empty);
            Assert.That(charges.First().Cost, Is.EqualTo(10.0M));
            Assert.That(charges.First().EnergyKwh, Is.EqualTo(15.0M));
            Assert.That(charges.First().StartTime, Is.EqualTo(from));
            Assert.That(charges.First().EndTime, Is.EqualTo(to));
            rateLimitHelperMock.Verify(x => x.AddRequest(), Times.Exactly(2));
        }
    }
}
