using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Tenants.Requests;
using HookSentry.Api.DataTransfer.Tenants.Responses;
using HookSentry.Domain;
using HookSentry.Domain.Tenants;

namespace HookSentry.Api.Features.Tenants.UpdateTenant;

public class UpdateTenantEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/v1/tenants/{id:guid}", Handle)
            .WithName("UpdateTenant")
            .WithTags("Tenants")
            .WithSummary("Updates tenant settings")
            .WithDescription("""
                Partially updates the tenant's name and webhook delivery settings.
                Only admin users may call this endpoint.

                **Route parameters:**
                - `id` *(required)*: tenant UUID

                **Body** *(all fields optional):*
                - `name`: new unique tenant name
                - `maxTrys`: max delivery attempts before opening the circuit breaker (minimum: 1)
                - `circuitBreakerTimer`: seconds the circuit stays open before a half-open probe (minimum: 1)

                **Return codes:**
                - `200 OK`: tenant updated
                - `400 Bad Request`: invalid value
                - `401 Unauthorized`: missing or invalid token
                - `403 Forbidden`: caller is not an admin or the tenant belongs to another account
                - `404 Not Found`: tenant not found
                - `409 Conflict`: name is already taken by another tenant
                """)
            .RequireAuthorization()
            .Produces<TenantResponse>()
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<string>(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> Handle(
        Guid id,
        UpdateTenantRequest request,
        ClaimsPrincipal user,
        ITenantRepository tenantRepository,
        IUnitOfWorkFactory uowFactory,
        ILogger<UpdateTenantEndpoint> logger,
        CancellationToken ct)
    {
        if (user.RequireAdminRole(out var tenantId) is { } err) return err;

        if (tenantId != id)
            return Results.Forbid();

        var tenant = await tenantRepository.FindAsync(id, ct);

        if (tenant is null)
            return Results.NotFound();

        await using var uow = uowFactory.Create();

        try
        {
            if (request.Name is not null)
            {
                if (await tenantRepository.NameExistsAsync(request.Name.Trim(), ct))
                    return Results.Conflict($"Name '{request.Name}' is already taken.");

                tenant.Rename(request.Name);
            }

            if (request.MaxTrys.HasValue || request.CircuitBreakerTimer.HasValue)
            {
                tenant.UpdateSettings(
                    request.MaxTrys ?? tenant.MaxTrys,
                    request.CircuitBreakerTimer ?? tenant.CircuitBreakerTimer);
            }
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        await uow.CommitAsync(ct);

        logger.LogInformation(
            "Tenant updated. TenantId={TenantId} ActorEmail={ActorEmail}",
            tenantId, user.GetEmail());

        return Results.Ok(new TenantResponse(
            tenant.Id,
            tenant.Name,
            tenant.MaxTrys,
            tenant.CircuitBreakerTimer,
            tenant.CreatedAt,
            tenant.UpdatedAt));
    }
}
