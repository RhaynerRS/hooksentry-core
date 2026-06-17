using StackExchange.Redis;

namespace HookSentry.Infrastructure.Auth;

public sealed class RedisJwtDenylist(IConnectionMultiplexer redis) : IJwtDenylist
{
    private const string KeyPrefix = "auth:jwt:revoked:";

    public async Task RevokeAsync(string jti, TimeSpan remainingTtl, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync($"{KeyPrefix}{jti}", "1", remainingTtl);
    }

    public async Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        return await db.KeyExistsAsync($"{KeyPrefix}{jti}");
    }
}
