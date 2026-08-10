using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Helpers.Interfaces;
using TeslaMateAgile.Services.Interfaces;

namespace TeslaMateAgile.Helpers;

public class RateLimitHelper : IRateLimitHelper
{
    private readonly ILogger<RateLimitHelper> _logger;
    private readonly TeslaMateOptions _teslaMateOptions;

    private readonly TimeProvider _timeProvider;
    private int _rateLimitMaxRequests;
    private int _rateLimitPeriodSeconds;
    private int _currentRequestCount = 0;
    private int _requestsForCurrentCharge = 0;
    private DateTimeOffset _periodStartTime;

    public RateLimitHelper(
        ILogger<RateLimitHelper> logger,
        IOptions<TeslaMateOptions> teslaMateOptions,
        TimeProvider timeProvider
       )
    {
        _logger = logger;
        _teslaMateOptions = teslaMateOptions.Value;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _rateLimitMaxRequests = _teslaMateOptions.RateLimitMaxRequests;
        _rateLimitPeriodSeconds = _teslaMateOptions.RateLimitPeriodSeconds;
        _periodStartTime = _timeProvider.GetUtcNow();
    }

    public IRateLimitHelper Configure(IRateLimitedService rateLimitedService)
    {
        _rateLimitMaxRequests = _teslaMateOptions.RateLimitMaxRequests > 0
            ? _teslaMateOptions.RateLimitMaxRequests
            : (rateLimitedService?.DefaultRateLimitMaxRequests ?? 0);
        _rateLimitPeriodSeconds = _teslaMateOptions.RateLimitPeriodSeconds > 0
            ? _teslaMateOptions.RateLimitPeriodSeconds
            : (rateLimitedService?.DefaultRateLimitPeriodSeconds ?? 0);
        return this;
    }

    public void BeginChargeCalculation()
    {
        _requestsForCurrentCharge = 0;
    }

    public void AddRequest()
    {
        if (!Check()) return;
        _requestsForCurrentCharge++;
        if (_currentRequestCount + 1 > _rateLimitMaxRequests)
        {
            // Asking for more than a whole period allows is a configuration fault rather than the back
            // pressure the limit exists to apply, and no number of retries will get past it. Only this
            // charge is claimed to be stuck: how many requests a charge costs varies by provider and by
            // the period the charge spans, so other charges may well be priced under the same limit.
            if (_requestsForCurrentCharge > _rateLimitMaxRequests)
            {
                _logger.LogError("This charge asked for more requests from the energy provider than the rate limit allows in one whole period, so it cannot be priced however often it is retried. Raise TeslaMate__RateLimitMaxRequests to at least {RequestsRequired}, it is currently {RateLimitMaxRequests} per {RateLimitPeriodSeconds} second(s)", _requestsForCurrentCharge, _rateLimitMaxRequests, _rateLimitPeriodSeconds);
            }
            throw new RateLimitException();
        }
        _currentRequestCount++;
    }

    public bool HasReachedRateLimit()
    {
        if (!Check()) return false;
        return _currentRequestCount >= _rateLimitMaxRequests;
    }

    public DateTimeOffset GetNextReset()
    {
        if (!Check()) throw new InvalidOperationException();
        var nextReset = _periodStartTime.AddSeconds(_rateLimitPeriodSeconds);
        return nextReset;
    }

    private bool Check()
    {
        if (_rateLimitMaxRequests <= 0 || _rateLimitPeriodSeconds <= 0)
        {
            _logger.LogDebug("Rate limiting is disabled");
            return false;
        }
        var now = _timeProvider.GetUtcNow();
        var elapsedSeconds = (now - _periodStartTime).TotalSeconds;
        if (elapsedSeconds > _rateLimitPeriodSeconds)
        {
            _logger.LogDebug("Rate limit period has elapsed. Resetting request count");
            _periodStartTime = now;
            _currentRequestCount = 0;
            _requestsForCurrentCharge = 0;
        }
        return true;
    }
}
