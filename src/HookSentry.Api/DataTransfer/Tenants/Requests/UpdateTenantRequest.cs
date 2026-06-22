namespace HookSentry.Api.DataTransfer.Tenants.Requests;

public record UpdateTenantRequest(
    string? Name = null,
    int? MaxTrys = null,
    int? CircuitBreakerTimer = null);
