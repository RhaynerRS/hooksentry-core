using HookSentry.Domain;
using NHibernate;

namespace HookSentry.Infrastructure.Persistence;

public sealed class NHibernateUnitOfWork : IUnitOfWork
{
    private readonly ITransaction _transaction;

    internal NHibernateUnitOfWork(ISession session)
    {
        _transaction = session.BeginTransaction();
    }

    public Task CommitAsync(CancellationToken ct = default)
        => _transaction.CommitAsync(ct);

    public ValueTask DisposeAsync()
    {
        _transaction.Dispose();
        return ValueTask.CompletedTask;
    }
}
