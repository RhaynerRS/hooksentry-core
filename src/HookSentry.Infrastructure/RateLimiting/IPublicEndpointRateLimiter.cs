namespace HookSentry.Infrastructure.RateLimiting;

public interface IPublicEndpointRateLimiter
{
    Task<bool> IsBlockedAsync(string endpoint, string key, CancellationToken ct = default);
    Task RecordAsync(string endpoint, string key, CancellationToken ct = default);
    Task ResetAsync(string endpoint, string key, CancellationToken ct = default);
}
