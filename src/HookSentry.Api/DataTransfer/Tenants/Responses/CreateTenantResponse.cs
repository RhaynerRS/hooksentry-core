using HookSentry.Domain.Users;

namespace HookSentry.Api.DataTransfer.Tenants.Responses;

public record CreateTenantResponse(
    Guid TenantId,
    string TenantName,
    string WebhookSecret,
    int MaxTrys,
    int CircuitBreakerTimer,
    DateTimeOffset TenantCreatedAt,
    Guid OwnerUserId,
    string OwnerEmail,
    UserRole OwnerRole,
    DateTimeOffset OwnerCreatedAt);
