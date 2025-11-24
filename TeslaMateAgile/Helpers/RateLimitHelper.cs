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

    private int _rateLimitMaxRequests;
    private int _rateLimitPeriodSeconds;
    private int _currentRequestCount = 0;
    private DateTimeOffset _periodStartTime = DateTime.UtcNow;

    public RateLimitHelper(
        ILogger<RateLimitHelper> logger,
        IOptions<TeslaMateOptions> teslaMateOptions
       )
    {
        _logger = logger;
        _teslaMateOptions = teslaMateOptions.Value;
        _rateLimitMaxRequests = _teslaMateOptions.RateLimitMaxRequests;
        _rateLimitPeriodSeconds = _teslaMateOptions.RateLimitPeriodSeconds;
    }

    public IRateLimitHelper Configure(IRateLimitedService rateLimitedService)
    {
        _rateLimitMaxRequests = rateLimitedService?.DefaultRateLimitMaxRequests ?? _teslaMateOptions.RateLimitMaxRequests;
        _rateLimitPeriodSeconds = rateLimitedService?.DefaultRateLimitPeriodSeconds ?? _teslaMateOptions.RateLimitPeriodSeconds;
        return this;
    }

    public void AddRequest()
    {
        if (!Check()) return;
        if (_currentRequestCount + 1 > _rateLimitMaxRequests)
        {
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
        var now = DateTime.UtcNow;
        var elapsedSeconds = (now - _periodStartTime).TotalSeconds;
        if (elapsedSeconds > _rateLimitPeriodSeconds)
        {
            _logger.LogDebug("Rate limit period has elapsed. Resetting request count");
            _periodStartTime = now;
            _currentRequestCount = 0;
        }
        return true;
    }
}
