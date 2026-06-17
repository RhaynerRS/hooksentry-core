namespace HookSentry.Infrastructure.Auth;

public interface IJwtDenylist
{
    Task RevokeAsync(string jti, TimeSpan remainingTtl, CancellationToken ct = default);
    Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default);
}
