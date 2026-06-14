using System.Text.Json;
using StackExchange.Redis;

namespace HookSentry.Infrastructure.Destinations;

public sealed class DestinationCacheService(IConnectionMultiplexer redis) : IDestinationCacheService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<DestinationCacheEntry?> GetAsync(Guid destinationId, CancellationToken ct = default)
    {
        var value = await _db.StringGetAsync(CacheKey(destinationId));
        return value.HasValue
            ? JsonSerializer.Deserialize<DestinationCacheEntry>(value.ToString())
            : null;
    }

    public Task SetAsync(Guid destinationId, DestinationCacheEntry entry, CancellationToken ct = default)
        => _db.StringSetAsync(CacheKey(destinationId), JsonSerializer.Serialize(entry), Ttl);

    public Task RemoveAsync(Guid destinationId, CancellationToken ct = default)
        => _db.KeyDeleteAsync(CacheKey(destinationId));

    private static string CacheKey(Guid id) => $"destination:{id}";
}
