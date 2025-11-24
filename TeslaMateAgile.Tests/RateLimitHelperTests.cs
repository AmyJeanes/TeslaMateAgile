using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NUnit.Framework;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Helpers;
using TeslaMateAgile.Services.Interfaces;

namespace TeslaMateAgile.Tests;

public class RateLimitHelperTests
{
    private Mock<ILogger<RateLimitHelper>> _loggerMock;
    private FakeTimeProvider _timeProvider;
    private TeslaMateOptions _options;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<RateLimitHelper>>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        _options = new TeslaMateOptions
        {
            RateLimitMaxRequests = 3,
            RateLimitPeriodSeconds = 60
        };
    }

    [Test]
    public void AddRequest_ThrowsAfterConfiguredLimit()
    {
        var subject = CreateSubject();

        subject.AddRequest();
        subject.AddRequest();
        subject.AddRequest();

        Assert.That(() => subject.AddRequest(), Throws.TypeOf<RateLimitException>());
    }

    [Test]
    public void HasReachedRateLimit_ResetsAfterPeriodElapses()
    {
        var subject = CreateSubject();

        subject.AddRequest();
        subject.AddRequest();
        subject.AddRequest();
        Assert.That(subject.HasReachedRateLimit(), Is.True);

        _timeProvider.Advance(TimeSpan.FromSeconds(61));

        Assert.That(subject.HasReachedRateLimit(), Is.False);
        subject.AddRequest();

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Rate limit period has elapsed. Resetting request count")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Test]
    public void Configure_UsesServiceDefaults()
    {
        var subject = CreateSubject();
        subject.Configure(new TestRateLimitedService());

        for (var i = 0; i < TestRateLimitedService.DefaultRequests; i++)
        {
            subject.AddRequest();
        }

        Assert.That(subject.HasReachedRateLimit(), Is.True);
        Assert.That(subject.GetNextReset(), Is.EqualTo(_timeProvider.GetUtcNow().AddSeconds(TestRateLimitedService.DefaultPeriodSeconds)));

        _timeProvider.Advance(TimeSpan.FromSeconds(TestRateLimitedService.DefaultPeriodSeconds + 1));

        Assert.That(subject.HasReachedRateLimit(), Is.False);
    }

    private RateLimitHelper CreateSubject()
    {
        return new RateLimitHelper(_loggerMock.Object, Options.Create(_options), _timeProvider);
    }

    private class TestRateLimitedService : IRateLimitedService
    {
        public const int DefaultRequests = 5;
        public const int DefaultPeriodSeconds = 10;

        public int DefaultRateLimitMaxRequests => DefaultRequests;
        public int DefaultRateLimitPeriodSeconds => DefaultPeriodSeconds;
    }
}
