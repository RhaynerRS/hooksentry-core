namespace HookSentry.Infrastructure.Events;

public interface IEventIdempotencyStore
{
    Task<Guid?> FindEventIdAsync(Guid tenantId, string key, CancellationToken ct = default);
    Task StoreAsync(Guid tenantId, string key, Guid eventId, CancellationToken ct = default);
}
