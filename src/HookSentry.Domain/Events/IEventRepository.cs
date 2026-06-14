using HookSentry.Domain.Repositories;

namespace HookSentry.Domain.Events;

public interface IEventRepository : IRepository<Event>
{
    Task<Event?> FindByIdempotencyKeyAsync(Guid tenantId, string key, CancellationToken ct = default);
}
