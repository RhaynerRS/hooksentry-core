using HookSentry.Domain.Repositories;

namespace HookSentry.Domain.Destinations;

public interface IDestinationUrlRepository : IRepository<DestinationUrl>
{
    Task<DestinationUrl?> FindByIngestTokenHashAsync(string tokenHash, CancellationToken ct = default);
}
