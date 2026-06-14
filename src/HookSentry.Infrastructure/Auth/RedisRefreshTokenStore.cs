using StackExchange.Redis;

namespace HookSentry.Infrastructure.Auth;

public sealed class RedisRefreshTokenStore(IConnectionMultiplexer redis) : IRefreshTokenStore
{
    private const string KeyPrefix = "auth:refresh:";
    private static readonly TimeSpan Ttl = TimeSpan.FromDays(7);

    public async Task StoreAsync(string token, Guid userId, Guid tenantId)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync($"{KeyPrefix}{token}", $"{userId}|{tenantId}", Ttl);
    }

    public async Task<(Guid UserId, Guid TenantId)?> GetAsync(string token)
    {
        var db = redis.GetDatabase();
        var value = await db.StringGetAsync($"{KeyPrefix}{token}");
        return Parse(value);
    }

    public async Task<(Guid UserId, Guid TenantId)?> ConsumeAsync(string token)
    {
        var db = redis.GetDatabase();
        var value = await db.StringGetDeleteAsync($"{KeyPrefix}{token}");
        return Parse(value);
    }

    public async Task RemoveAsync(string token)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync($"{KeyPrefix}{token}");
    }

    private static (Guid UserId, Guid TenantId)? Parse(RedisValue value)
    {
        if (!value.HasValue) return null;
        var parts = ((string)value!).Split('|');
        if (parts.Length != 2
            || !Guid.TryParse(parts[0], out var userId)
            || !Guid.TryParse(parts[1], out var tenantId))
            return null;
        return (userId, tenantId);
    }
}
