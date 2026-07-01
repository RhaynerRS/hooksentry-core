using Microsoft.Extensions.Options;
using StackExchange.Redis;
using HookSentry.Infrastructure.AbuseProtection;

namespace HookSentry.Infrastructure.RateLimiting;

public sealed class RedisPublicEndpointRateLimiter(
    IConnectionMultiplexer redis,
    IOptions<RegistrationAbuseOptions> abuseOptions) : IPublicEndpointRateLimiter
{
    private const string KeyPrefix = "ratelimit:public:";

    private static readonly Dictionary<string, TimeSpan> Windows = new()
    {
        ["login"]                = TimeSpan.FromMinutes(5),
        ["create-tenant"]        = TimeSpan.FromHours(1),
        ["register-with-invite"] = TimeSpan.FromHours(1),
    };

    private static readonly Dictionary<string, int> DefaultLimits = new()
    {
        ["login"]                = 10,
        ["create-tenant"]        = 5,
        ["register-with-invite"] = 10,
    };

    private int GetLimit(string endpoint)
    {
        if (endpoint == "create-tenant" && abuseOptions.Value.MaxRegistrationsPerIpPerHour > 0)
            return abuseOptions.Value.MaxRegistrationsPerIpPerHour;
        return DefaultLimits.TryGetValue(endpoint, out var limit) ? limit : int.MaxValue;
    }

    public async Task<bool> IsBlockedAsync(string endpoint, string key, CancellationToken ct = default)
    {
        if (!Windows.ContainsKey(endpoint)) return false;
        try
        {
            var value = await redis.GetDatabase().StringGetAsync(Key(endpoint, key));
            return value.HasValue && (long)value >= GetLimit(endpoint);
        }
        catch { return false; } // fail open — never block registration on Redis error
    }

    public async Task RecordAsync(string endpoint, string key, CancellationToken ct = default)
    {
        if (!Windows.TryGetValue(endpoint, out var window)) return;
        try
        {
            var db = redis.GetDatabase();
            var redisKey = Key(endpoint, key);
            var count = await db.StringIncrementAsync(redisKey);
            if (count == 1)
                await db.KeyExpireAsync(redisKey, window);
        }
        catch { /* fail open */ }
    }

    public async Task ResetAsync(string endpoint, string key, CancellationToken ct = default)
    {
        try { await redis.GetDatabase().KeyDeleteAsync(Key(endpoint, key)); }
        catch { /* fail open */ }
    }

    private static string Key(string endpoint, string key) => $"{KeyPrefix}{endpoint}:{key}";
}
