using HookSentry.Domain.Events;
using NHibernate;
using NHibernate.Linq;

namespace HookSentry.Infrastructure.Persistence.Repositories;

public sealed class EventRepository(ISession session)
    : NHibernateRepository<Event>(session), IEventRepository
{
    public async Task<Event?> FindByIdempotencyKeyAsync(Guid tenantId, string key, CancellationToken ct = default)
        => await Session.Query<Event>()
            .Where(e => e.TenantId == tenantId && e.IdempotencyKey == key)
            .SingleOrDefaultAsync(ct);
}
