using TeslaMateAgile.Services.Interfaces;

namespace TeslaMateAgile.Helpers.Interfaces;

public interface IRateLimitHelper
{
    IRateLimitHelper Configure(IRateLimitedService rateLimitedService);

    /// <summary>
    /// Marks the start of the requests belonging to one charge calculation, so that a limit too small
    /// to fit that calculation at all can be told apart from a limit earlier calculations have used up.
    /// </summary>
    void BeginChargeCalculation();

    void AddRequest();
    bool HasReachedRateLimit();
    DateTimeOffset GetNextReset();
}
