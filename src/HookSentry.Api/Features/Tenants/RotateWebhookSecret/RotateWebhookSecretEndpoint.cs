using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Tenants.Responses;
using HookSentry.Domain;
using HookSentry.Domain.Tenants;

namespace HookSentry.Api.Features.Tenants.RotateWebhookSecret;

public class RotateWebhookSecretEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/tenants/{id:guid}/webhook-secret", Handle)
            .WithName("RegenerateWebhookSecret")
            .WithTags("Tenants")
            .WithSummary("Rotates the webhook secret for the tenant")
            .WithDescription("""
                Generates a new HMAC-SHA256 webhook secret and invalidates the previous one.
                All destination servers must be updated with the new secret to continue verifying
                the X-HookSentry-Signature header on incoming webhooks.

                **Route parameters:**
                - `id` *(required)*: tenant UUID

                **Return codes:**
                - `200 OK`: new webhook secret
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
        IUnitOfWorkFactory uowFactory,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;
        if (tenantId != id) return Results.Forbid();

        await using var uow = uowFactory.Create();

        var tenant = await tenantRepository.FindAsync(id, ct);
        if (tenant is null) return Results.NotFound();

        tenant.RotateWebhookSecret();

        await uow.CommitAsync(ct);

        return Results.Ok(new WebhookSecretResponse(tenant.WebhookSecret));
    }
}
