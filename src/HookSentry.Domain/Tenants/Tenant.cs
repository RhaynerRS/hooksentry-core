using System.Security.Cryptography;

namespace HookSentry.Domain.Tenants;

public class Tenant
{
    public virtual Guid Id { get; protected set; }
    public virtual string Name { get; protected set; } = default!;
    public virtual string WebhookSecret { get; protected set; } = default!;
    public virtual int MaxTrys { get; protected set; }
    public virtual int CircuitBreakerTimer { get; protected set; }
    public virtual DateTimeOffset CreatedAt { get; protected set; }
    public virtual DateTimeOffset UpdatedAt { get; protected set; }

    protected Tenant() { }

    public Tenant(string name, int maxTrys = 10, int circuitBreakerTimer = 300)
    {
        Id = Guid.NewGuid();
        Name = name;
        WebhookSecret = GenerateWebhookSecret();
        MaxTrys = maxTrys;
        CircuitBreakerTimer = circuitBreakerTimer;
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    public virtual void UpdateSettings(int maxTrys, int circuitBreakerTimer)
    {
        if (maxTrys < 1)
            throw new ArgumentOutOfRangeException(nameof(maxTrys), "MaxTrys deve ser no mínimo 1.");
        if (circuitBreakerTimer < 1)
            throw new ArgumentOutOfRangeException(nameof(circuitBreakerTimer), "CircuitBreakerTimer deve ser no mínimo 1 segundo.");

        MaxTrys = maxTrys;
        CircuitBreakerTimer = circuitBreakerTimer;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public virtual void RotateWebhookSecret()
    {
        WebhookSecret = GenerateWebhookSecret();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string GenerateWebhookSecret() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
}
