namespace HookSentry.Api.Common.Tenants;

/// <summary>
/// Extension point invoked by <c>POST /api/v1/tenants</c> around tenant creation.
/// The OSS core ships zero guards, so self-hosted behavior is unchanged; the cloud
/// host registers guards (anti-abuse) that can veto a registration or record it after
/// a successful commit. Mirrors the <c>ITenantCreatedPostProcessor</c> pattern.
/// </summary>
public interface ITenantCreationGuard
{
    /// <summary>
    /// Runs before the tenant is created. Return a non-null <see cref="IResult"/> to
    /// reject the registration (its status code/body is returned as-is), or <c>null</c>
    /// to allow it to proceed.
    /// </summary>
    Task<IResult?> CheckAsync(TenantCreationContext ctx, CancellationToken ct);

    /// <summary>
    /// Runs after the tenant is successfully committed, so a guard can record state
    /// (e.g. associate a device fingerprint with the new tenant).
    /// </summary>
    Task RecordAsync(TenantCreationContext ctx, Guid tenantId, CancellationToken ct);
}

/// <summary>Immutable snapshot of the registration attempt passed to each guard.</summary>
public sealed record TenantCreationContext(
    string Name,
    string OwnerEmail,
    string? DeviceFingerprint,
    string? CfTurnstileToken,
    string ClientIp);
