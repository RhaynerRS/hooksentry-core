using System.Security.Cryptography;
using System.Text;

namespace HookSentry.Domain.ApiKeys;

public class ApiKey
{
    public virtual Guid Id { get; protected set; }
    public virtual Guid TenantId { get; protected set; }
    public virtual string KeyHash { get; protected set; } = default!;
    public virtual string Name { get; protected set; } = default!;
    public virtual bool IsActive { get; protected set; }
    public virtual DateTimeOffset CreatedAt { get; protected set; }
    public virtual DateTimeOffset? RevokedAt { get; protected set; }

    protected ApiKey() { }

    public ApiKey(Guid tenantId, string name)
    {
        SetTenantId(tenantId);
        SetName(name);

        var raw = GenerateRawKey();
        KeyHash = ComputeHash(raw);
        IsActive = true;
        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;

        RawKey = raw;
    }

    // Populated only during creation — not persisted
    public string? RawKey { get; private set; }

    public virtual void Revoke()
    {
        if (!IsActive)
            throw new InvalidOperationException("API key is already revoked.");
        IsActive = false;
        RevokedAt = DateTimeOffset.UtcNow;
    }

    public virtual void SetTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        TenantId = tenantId;
    }

    public virtual void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty.", nameof(name));
        if (name.Length > 100)
            throw new ArgumentException("Name cannot exceed 100 characters.", nameof(name));
        Name = name;
    }

    public static string ComputeHash(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexStringLower(bytes);
    }

    private static string GenerateRawKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var random = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"hsk_{random}";
    }
}
