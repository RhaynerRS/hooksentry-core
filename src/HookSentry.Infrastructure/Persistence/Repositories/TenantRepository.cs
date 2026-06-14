using HookSentry.Domain.Tenants;
using NHibernate;
using NHibernate.Linq;

namespace HookSentry.Infrastructure.Persistence.Repositories;

public sealed class TenantRepository(ISession session)
    : NHibernateRepository<Tenant>(session), ITenantRepository
{
    public Task<bool> NameExistsAsync(string name, CancellationToken ct = default)
        => Session.Query<Tenant>()
            .AnyAsync(t => t.Name == name, ct);
}
