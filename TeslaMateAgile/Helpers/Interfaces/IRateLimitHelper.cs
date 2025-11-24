namespace TeslaMateAgile.Helpers.Interfaces;

public interface IRateLimitHelper
{
    void AddRequest();
    bool HasReachedRateLimit();
}
