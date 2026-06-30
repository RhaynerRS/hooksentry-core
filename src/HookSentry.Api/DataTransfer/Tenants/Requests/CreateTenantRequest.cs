namespace HookSentry.Api.DataTransfer.Tenants.Requests;

public record CreateTenantRequest(
    string Name,
    string OwnerEmail,
    string OwnerPassword,
    int MaxTrys = 10,
    int CircuitBreakerTimer = 300);
