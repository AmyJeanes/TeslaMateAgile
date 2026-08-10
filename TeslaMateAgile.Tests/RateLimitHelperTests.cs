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
        _options.RateLimitMaxRequests = 0;
        _options.RateLimitPeriodSeconds = 0;
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

    [Test]
    public void Configure_GlobalOptionsOverrideServiceDefaults()
    {
        var subject = CreateSubject();
        subject.Configure(new TestRateLimitedService());

        for (var i = 0; i < _options.RateLimitMaxRequests; i++)
        {
            subject.AddRequest();
        }

        Assert.That(subject.HasReachedRateLimit(), Is.True);
        Assert.That(subject.GetNextReset(), Is.EqualTo(_timeProvider.GetUtcNow().AddSeconds(_options.RateLimitPeriodSeconds)));
    }

    /// <summary>
    /// A charge that on its own asks for more requests than a whole period allows can never be priced
    /// however often it is retried, so it is a configuration fault and must be reported as one rather
    /// than looking like the back pressure the limit exists to apply.
    /// </summary>
    [Test]
    public void AddRequest_LogsErrorWhenOneChargeCannotFitTheLimit()
    {
        _options.RateLimitMaxRequests = 2;
        var subject = CreateSubject();

        subject.BeginChargeCalculation();
        subject.AddRequest();
        subject.AddRequest();

        Assert.That(() => subject.AddRequest(), Throws.TypeOf<RateLimitException>());

        VerifyErrorLogged(Times.Once(), "Raise TeslaMate__RateLimitMaxRequests to at least 3", "currently 2 per 60 second(s)");
    }

    /// <summary>
    /// The boundary matters more than either side of it: a charge needing exactly the whole limit is
    /// priceable, so reporting it would send every correctly configured user chasing a setting that is
    /// already right.
    /// </summary>
    [Test]
    public void AddRequest_DoesNotLogErrorWhenAChargeNeedsExactlyTheWholeLimit()
    {
        _options.RateLimitMaxRequests = 3;
        var subject = CreateSubject();

        // an earlier charge takes a request, so this one runs out one short of finishing even though
        // three requests, the whole limit, would have been enough on a period of its own
        subject.BeginChargeCalculation();
        subject.AddRequest();

        subject.BeginChargeCalculation();
        subject.AddRequest();
        subject.AddRequest();

        Assert.That(() => subject.AddRequest(), Throws.TypeOf<RateLimitException>());

        VerifyErrorLogged(Times.Never());
    }

    /// <summary>
    /// The same exception is thrown when earlier charges have used the period up, but that is the limit
    /// working as intended, so nothing is wrong to report.
    /// </summary>
    [Test]
    public void AddRequest_DoesNotLogErrorWhenTheLimitIsSpentByEarlierCharges()
    {
        var subject = CreateSubject();

        subject.BeginChargeCalculation();
        subject.AddRequest();
        subject.AddRequest();

        subject.BeginChargeCalculation();
        subject.AddRequest();

        Assert.That(() => subject.AddRequest(), Throws.TypeOf<RateLimitException>());

        VerifyErrorLogged(Times.Never());
    }

    /// <summary>
    /// A calculation may span a period boundary, and the requests it made before the reset are no longer
    /// held against the limit, so they must not be held against the charge either. Otherwise how long the
    /// provider takes to answer decides whether the charge is reported as impossible.
    /// </summary>
    [Test]
    public void AddRequest_CountsOnlyTheRequestsMadeSinceThePeriodResetAgainstTheCharge()
    {
        _options.RateLimitMaxRequests = 2;
        var subject = CreateSubject();

        subject.BeginChargeCalculation();
        subject.AddRequest();
        subject.AddRequest();

        _timeProvider.Advance(TimeSpan.FromSeconds(61));

        subject.AddRequest();
        subject.AddRequest();

        Assert.That(() => subject.AddRequest(), Throws.TypeOf<RateLimitException>());

        // three requests since the reset, not the five the charge has made in total, otherwise how long
        // the provider takes to answer would change the number the user is told to configure
        VerifyErrorLogged(Times.Once(), "to at least 3");
    }

    private void VerifyErrorLogged(Times times, params string[] expectedFragments)
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => expectedFragments.All(f => v.ToString()!.Contains(f))),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            times);
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
