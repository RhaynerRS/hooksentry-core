namespace HookSentry.Infrastructure.Auth;

public interface ILoginRateLimiter
{
    Task<bool> IsBlockedAsync(string ip, string email, CancellationToken ct = default);
    Task RecordFailureAsync(string ip, string email, CancellationToken ct = default);
    Task ResetAsync(string ip, string email, CancellationToken ct = default);
}
