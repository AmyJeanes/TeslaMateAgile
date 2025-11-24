using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Helpers.Interfaces;
using TeslaMateAgile.Services.Interfaces;

namespace TeslaMateAgile.Helpers;

public class RateLimitHelper : IRateLimitHelper
{
    private readonly ILogger<RateLimitHelper> _logger;
    private readonly int _rateLimitMaxRequests;
    private readonly int _rateLimitPeriodSeconds;

    private int _currentRequestCount = 0;
    private DateTime _periodStartTime = DateTime.UtcNow;

    public RateLimitHelper(
        ILogger<RateLimitHelper> logger,
        IOptions<TeslaMateOptions> teslaMateOptions,
        IPriceDataService priceDataService
       )
    {
        _logger = logger;
        var rateLimitedService = priceDataService as IRateLimitedService;
        _rateLimitMaxRequests = rateLimitedService?.DefaultRateLimitMaxRequests ?? teslaMateOptions.Value.RateLimitMaxRequests;
        _rateLimitPeriodSeconds = rateLimitedService?.DefaultRateLimitPeriodSeconds ?? teslaMateOptions.Value.RateLimitPeriodSeconds;
    }

    public void AddRequest()
    {
        CheckPeriod();
        if (_currentRequestCount + 1 >= _rateLimitMaxRequests)
        {
            throw new RateLimitException();
        }
        _currentRequestCount++;
    }

    public bool HasReachedRateLimit()
    {
        CheckPeriod();
        return _currentRequestCount >= _rateLimitMaxRequests;
    }

    private void CheckPeriod()
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = (now - _periodStartTime).TotalSeconds;
        if (elapsedSeconds > _rateLimitPeriodSeconds)
        {
            _logger.LogDebug("Rate limit period has elapsed. Resetting request count");
            _periodStartTime = now;
            _currentRequestCount = 0;
        }
    }
}
