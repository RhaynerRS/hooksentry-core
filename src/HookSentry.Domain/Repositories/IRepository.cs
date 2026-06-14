namespace HookSentry.Domain.Repositories;

public interface IRepository<T> where T : class
{
    Task<T?> FindAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    Task RemoveAsync(T entity, CancellationToken ct = default);
    IQueryable<T> Query();
}
