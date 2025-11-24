using TeslaMateAgile.Services.Interfaces;

namespace TeslaMateAgile.Helpers.Interfaces;

public interface IRateLimitHelper
{
    IRateLimitHelper Configure(IRateLimitedService rateLimitedService);
    void AddRequest();
    bool HasReachedRateLimit();
    DateTimeOffset GetNextReset();
}
