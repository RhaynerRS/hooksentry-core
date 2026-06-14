using HookSentry.Domain.Common;

namespace HookSentry.Domain.Destinations;

public class DestinationUrl
{
    public virtual Guid Id { get; protected set; }
    public virtual Guid TenantId { get; protected set; }
    public virtual string Url { get; protected set; } = default!;
    public virtual DestinationUrlStatus Status { get; protected set; }
    public virtual int ServerRateLimit { get; protected set; }
    public virtual DestinationAuthType? AuthType { get; protected set; }
    public virtual string? CredentialsEncrypted { get; protected set; }
    public virtual string? IngestTokenHash { get; protected set; }
    public virtual DateTimeOffset CreatedAt { get; protected set; }
    public virtual DateTimeOffset UpdatedAt { get; protected set; }

    protected DestinationUrl() { }

    public DestinationUrl(Guid tenantId, string url, int serverRateLimit = 5)
    {
        SetTenantId(tenantId);
        ValidateUrl(url);
        ValidateServerRateLimit(serverRateLimit);

        Id = Guid.NewGuid();
        Url = url;
        ServerRateLimit = serverRateLimit;
        Status = DestinationUrlStatus.Active;
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    public virtual bool IsActive() => Status == DestinationUrlStatus.Active;

    public virtual void Activate()
    {
        Status = DestinationUrlStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public virtual void Deactivate()
    {
        Status = DestinationUrlStatus.Inactive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public virtual void Suspend()
    {
        Status = DestinationUrlStatus.Suspended;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public virtual void SetUrl(string url)
    {
        ValidateUrl(url);
        Url = url;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public virtual void SetTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));

        TenantId = tenantId;
    }

    public virtual void SetServerRateLimit(int limit)
    {
        ValidateServerRateLimit(limit);
        ServerRateLimit = limit;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public virtual void SetAuth(DestinationAuthType? authType, string? credentialsEncrypted)
    {
        if (authType.HasValue && string.IsNullOrWhiteSpace(credentialsEncrypted))
            throw new ArgumentException(
                "Encrypted credentials are required when AuthType is set.", nameof(credentialsEncrypted));

        if (!authType.HasValue && !string.IsNullOrWhiteSpace(credentialsEncrypted))
            throw new ArgumentException(
                "AuthType is required when credentials are provided.", nameof(authType));

        AuthType = authType;
        CredentialsEncrypted = credentialsEncrypted;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public virtual string RotateIngestToken()
    {
        var (rawToken, hash) = IngestToken.Generate(IngestToken.DestinationPrefix);
        IngestTokenHash = hash;
        UpdatedAt = DateTimeOffset.UtcNow;
        return rawToken;
    }

    private static void ValidateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be null or empty.", nameof(url));

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("URL must be a valid HTTPS address.", nameof(url));
    }

    private static void ValidateServerRateLimit(int limit)
    {
        if (limit < 1)
            throw new ArgumentOutOfRangeException(nameof(limit), "ServerRateLimit must be at least 1.");
    }
}
