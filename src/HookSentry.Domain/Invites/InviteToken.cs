using System.Security.Cryptography;

namespace HookSentry.Domain.Invites;

public class InviteToken
{
    public virtual Guid Id { get; protected set; }
    public virtual Guid TenantId { get; protected set; }
    public virtual string Token { get; protected set; } = default!;
    public virtual DateTimeOffset ExpiresAt { get; protected set; }
    public virtual DateTimeOffset? UsedAt { get; protected set; }
    public virtual InviteTokenStatus Status { get; protected set; }
    public virtual DateTimeOffset CreatedAt { get; protected set; }
    public virtual DateTimeOffset UpdatedAt { get; protected set; }

    protected InviteToken() { }

    public InviteToken(Guid tenantId, int validityDays = 7)
    {
        SetTenantId(tenantId);
        SetValidityDays(validityDays);
        Token = GenerateSecureToken();
        Status = InviteTokenStatus.Pending;
        Id = Guid.NewGuid();
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void SetTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        TenantId = tenantId;
    }

    private void SetValidityDays(int validityDays)
    {
        if (validityDays < 1)
            throw new ArgumentOutOfRangeException(nameof(validityDays), "Validity must be at least 1 day.");
        if (validityDays > 30)
            throw new ArgumentOutOfRangeException(nameof(validityDays), "Validity cannot exceed 30 days.");
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(validityDays);
    }

    public virtual void Use()
    {
        if (Status == InviteTokenStatus.Used)
            throw new InvalidOperationException("This invite has already been used.");
        if (DateTimeOffset.UtcNow > ExpiresAt)
            throw new InvalidOperationException("This invite has expired.");
        UsedAt = DateTimeOffset.UtcNow;
        Status = InviteTokenStatus.Used;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
