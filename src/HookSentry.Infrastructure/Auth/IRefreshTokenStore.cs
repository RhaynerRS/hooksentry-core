namespace HookSentry.Infrastructure.Auth;

public interface IRefreshTokenStore
{
    Task StoreAsync(string token, Guid userId, Guid tenantId);
    Task<(Guid UserId, Guid TenantId)?> GetAsync(string token);
    Task<(Guid UserId, Guid TenantId)?> ConsumeAsync(string token);
    Task RemoveAsync(string token);
}
