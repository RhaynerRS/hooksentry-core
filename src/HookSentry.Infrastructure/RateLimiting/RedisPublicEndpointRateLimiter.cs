using StackExchange.Redis;

namespace HookSentry.Infrastructure.RateLimiting;

public sealed class RedisPublicEndpointRateLimiter(IConnectionMultiplexer redis) : IPublicEndpointRateLimiter
{
    private const string KeyPrefix = "ratelimit:public:";

    private static readonly Dictionary<string, (int Limit, TimeSpan Window)> Policies = new()
    {
        ["login"]                = (10, TimeSpan.FromMinutes(5)),
        ["create-tenant"]        = (5,  TimeSpan.FromHours(1)),
        ["register-with-invite"] = (10, TimeSpan.FromHours(1)),
    };

    public async Task<bool> IsBlockedAsync(string endpoint, string key, CancellationToken ct = default)
    {
        if (!Policies.TryGetValue(endpoint, out var policy)) return false;
        var db = redis.GetDatabase();
        var value = await db.StringGetAsync(Key(endpoint, key));
        return value.HasValue && (long)value >= policy.Limit;
    }

    public async Task RecordAsync(string endpoint, string key, CancellationToken ct = default)
    {
        if (!Policies.TryGetValue(endpoint, out var policy)) return;
        var db = redis.GetDatabase();
        var redisKey = Key(endpoint, key);
        var count = await db.StringIncrementAsync(redisKey);
        if (count == 1)
            await db.KeyExpireAsync(redisKey, policy.Window);
    }

    public async Task ResetAsync(string endpoint, string key, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(Key(endpoint, key));
    }

    private static string Key(string endpoint, string key) => $"{KeyPrefix}{endpoint}:{key}";
}
