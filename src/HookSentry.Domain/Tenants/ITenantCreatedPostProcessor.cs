namespace HookSentry.Domain.Tenants;

public interface ITenantCreatedPostProcessor
{
    Task ProcessAsync(Guid tenantId, Guid adminUserId, CancellationToken ct);
}
