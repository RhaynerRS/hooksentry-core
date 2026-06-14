using HookSentry.Domain.ApiKeys;
using NHibernate;

namespace HookSentry.Infrastructure.Persistence.Repositories;

public sealed class ApiKeyRepository(ISession session)
    : NHibernateRepository<ApiKey>(session), IApiKeyRepository
{
}
