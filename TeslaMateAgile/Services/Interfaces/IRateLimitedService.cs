namespace TeslaMateAgile.Services.Interfaces;

public interface IRateLimitedService
{
    int DefaultRateLimitMaxRequests { get; }
    int DefaultRateLimitPeriodSeconds { get; }
}
