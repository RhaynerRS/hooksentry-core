using HookSentry.Domain.Repositories;
using NHibernate;
using NHibernate.Linq;

namespace HookSentry.Infrastructure.Persistence.Repositories;

public class NHibernateRepository<T>(ISession session) : IRepository<T> where T : class
{
    protected readonly ISession Session = session;

    public async Task<T?> FindAsync(Guid id, CancellationToken ct = default)
        => await Session.GetAsync<T>(id, ct);

    public Task AddAsync(T entity, CancellationToken ct = default)
        => Session.SaveAsync(entity, ct);

    public Task RemoveAsync(T entity, CancellationToken ct = default)
        => Session.DeleteAsync(entity, ct);

    public IQueryable<T> Query() => Session.Query<T>();
}
