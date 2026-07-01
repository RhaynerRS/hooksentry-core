namespace HookSentry.Infrastructure.AbuseProtection;

public interface IDisposableEmailChecker
{
    Task<bool> IsDisposableAsync(string email, CancellationToken ct);
}
