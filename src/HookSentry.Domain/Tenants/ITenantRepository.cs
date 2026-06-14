using HookSentry.Domain.Repositories;

namespace HookSentry.Domain.Tenants;

public interface ITenantRepository : IRepository<Tenant>
{
    Task<bool> NameExistsAsync(string name, CancellationToken ct = default);
}
