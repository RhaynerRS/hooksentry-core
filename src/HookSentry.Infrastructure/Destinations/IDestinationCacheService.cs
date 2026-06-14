namespace HookSentry.Infrastructure.Destinations;

public interface IDestinationCacheService
{
    Task<DestinationCacheEntry?> GetAsync(Guid destinationId, CancellationToken ct = default);
    Task SetAsync(Guid destinationId, DestinationCacheEntry entry, CancellationToken ct = default);
    Task RemoveAsync(Guid destinationId, CancellationToken ct = default);
}
