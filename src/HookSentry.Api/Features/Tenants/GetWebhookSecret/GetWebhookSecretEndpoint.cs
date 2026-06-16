using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Tenants.Responses;
using HookSentry.Domain.Tenants;

namespace HookSentry.Api.Features.Tenants.GetWebhookSecret;

public class GetWebhookSecretEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/tenants/{id:guid}/webhook-secret", Handle)
            .WithName("GetWebhookSecret")
            .WithTags("Tenants")
            .WithSummary("Returns the webhook secret for the tenant")
            .WithDescription("""
                Returns the HMAC-SHA256 secret used to sign outbound webhooks delivered to destination URLs.

                **Route parameters:**
                - `id` *(required)*: tenant UUID

                **Return codes:**
                - `200 OK`: webhook secret
                - `401 Unauthorized`: missing or invalid token
                - `403 Forbidden`: tenant belongs to another user
                - `404 Not Found`: tenant not found
                """)
            .RequireAuthorization()
            .Produces<WebhookSecretResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid id,
        ClaimsPrincipal user,
        ITenantRepository tenantRepository,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;
        if (tenantId != id) return Results.Forbid();

        var tenant = await tenantRepository.FindAsync(id, ct);
        if (tenant is null) return Results.NotFound();

        return Results.Ok(new WebhookSecretResponse(tenant.WebhookSecret));
    }
}
