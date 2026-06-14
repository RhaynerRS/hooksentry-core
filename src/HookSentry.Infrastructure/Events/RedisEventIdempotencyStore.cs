using StackExchange.Redis;

namespace HookSentry.Infrastructure.Events;

public sealed class RedisEventIdempotencyStore(IConnectionMultiplexer redis) : IEventIdempotencyStore
{
    private const string KeyPrefix = "idempotency:";
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public async Task<Guid?> FindEventIdAsync(Guid tenantId, string key, CancellationToken ct = default)
    {
        var value = await redis.GetDatabase().StringGetAsync($"{KeyPrefix}{tenantId}:{key}");
        if (!value.HasValue || !Guid.TryParse(value.ToString(), out var eventId))
            return null;
        return eventId;
    }

    public Task StoreAsync(Guid tenantId, string key, Guid eventId, CancellationToken ct = default)
        => redis.GetDatabase().StringSetAsync($"{KeyPrefix}{tenantId}:{key}", eventId.ToString(), Ttl);
}
