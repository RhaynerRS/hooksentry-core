using HookSentry.Domain.Destinations;
using NHibernate;
using NHibernate.Linq;

namespace HookSentry.Infrastructure.Persistence.Repositories;

public sealed class DestinationUrlRepository(ISession session)
    : NHibernateRepository<DestinationUrl>(session), IDestinationUrlRepository
{
    public async Task<DestinationUrl?> FindByIngestTokenHashAsync(string tokenHash, CancellationToken ct = default)
        => await Session.Query<DestinationUrl>()
            .Where(d => d.IngestTokenHash == tokenHash)
            .SingleOrDefaultAsync(ct);
}
