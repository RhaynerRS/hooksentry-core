using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.DataTransfer.Tenants.Responses;
using HookSentry.Domain.Tenants;

namespace HookSentry.Api.Features.Tenants.GetTenant;

public class GetTenantEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/tenants/{id:guid}", Handle)
            .WithName("GetTenantById")
            .WithTags("Tenants")
            .WithSummary("Returns tenant data by ID")
            .WithDescription("""
                Looks up a tenant by their UUID.

                **Route parameters:**
                - `id` *(required)*: tenant UUID

                **Return codes:**
                - `200 OK`: tenant data (without `webhookSecret`)
                - `401 Unauthorized`: missing or invalid token
                - `404 Not Found`: tenant not found
                """)
            .RequireAuthorization()
            .Produces<TenantResponse>()
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid id,
        ITenantRepository tenantRepository,
        CancellationToken ct)
    {
        var tenant = await tenantRepository.FindAsync(id, ct);

        return tenant is null
            ? Results.NotFound()
            : Results.Ok(new TenantResponse(
                tenant.Id,
                tenant.Name,
                tenant.MaxTrys,
                tenant.CircuitBreakerTimer,
                tenant.CreatedAt,
                tenant.UpdatedAt));
    }
}
