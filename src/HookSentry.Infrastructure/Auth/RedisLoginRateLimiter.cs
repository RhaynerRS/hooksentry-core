using StackExchange.Redis;

namespace HookSentry.Infrastructure.Auth;

public sealed class RedisLoginRateLimiter(IConnectionMultiplexer redis) : ILoginRateLimiter
{
    private const string KeyPrefix = "auth:login:fail:";
    private const int MaxFailures = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    public async Task<bool> IsBlockedAsync(string ip, string email, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var results = await db.StringGetAsync([IpKey(ip), EmailKey(email)]);
        return results.Any(v => v.HasValue && (long)v >= MaxFailures);
    }

    public async Task RecordFailureAsync(string ip, string email, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        await Task.WhenAll(IncrementAsync(db, IpKey(ip)), IncrementAsync(db, EmailKey(email)));
    }

    public async Task ResetAsync(string ip, string email, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync([IpKey(ip), EmailKey(email)]);
    }

    private static async Task IncrementAsync(IDatabase db, string key)
    {
        var count = await db.StringIncrementAsync(key);
        // TTL is set only on first failure — window is fixed from that point
        if (count == 1)
            await db.KeyExpireAsync(key, Window);
    }

    private static string IpKey(string ip) => $"{KeyPrefix}ip:{ip}";
    private static string EmailKey(string email) => $"{KeyPrefix}email:{email}";
}
