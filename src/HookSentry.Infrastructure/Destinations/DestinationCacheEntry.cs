using HookSentry.Domain.Destinations;

namespace HookSentry.Infrastructure.Destinations;

public sealed record DestinationCacheEntry(
    Guid Id,
    string Url,
    DestinationUrlStatus Status,
    int ServerRateLimit,
    DestinationAuthType? AuthType,
    string? CredentialsEncrypted)
{
    public static DestinationCacheEntry From(DestinationUrl d) =>
        new(d.Id, d.Url, d.Status, d.ServerRateLimit, d.AuthType, d.CredentialsEncrypted);
}
