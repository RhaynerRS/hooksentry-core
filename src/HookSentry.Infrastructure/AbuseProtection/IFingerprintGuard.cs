namespace HookSentry.Infrastructure.AbuseProtection;

public interface IFingerprintGuard
{
    Task<FingerprintCheckResult> CheckAsync(string fingerprint, CancellationToken ct);
    Task RecordAsync(string fingerprint, Guid tenantId, CancellationToken ct);
}

public record FingerprintCheckResult(bool Blocked, int AccountCount, int Limit);
