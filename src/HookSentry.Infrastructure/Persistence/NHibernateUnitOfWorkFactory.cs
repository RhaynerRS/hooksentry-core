using HookSentry.Domain;
using NHibernate;

namespace HookSentry.Infrastructure.Persistence;

public sealed class NHibernateUnitOfWorkFactory(ISession session) : IUnitOfWorkFactory
{
    public IUnitOfWork Create() => new NHibernateUnitOfWork(session);
}
