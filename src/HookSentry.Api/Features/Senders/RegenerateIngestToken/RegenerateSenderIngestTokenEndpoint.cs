using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Senders.Responses;
using HookSentry.Domain;
using HookSentry.Domain.Senders;

namespace HookSentry.Api.Features.Senders.RegenerateIngestToken;

public class RegenerateSenderIngestTokenEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/senders/{id:guid}/ingest-token", Handle)
            .WithName("RegenerateSenderIngestToken")
            .WithTags("Senders")
            .WithSummary("Regenerates the ingest token of a sender")
            .WithDescription("""
                Generates a new ingest token for the specified sender, immediately invalidating the previous one.

                The returned token is shown **only once** — update the webhook configuration
                in the external service before closing this response.

                **Route parameters:**
                - `id` *(required)*: sender UUID

                **Return codes:**
                - `200 OK`: new ingest token generated
                - `401 Unauthorized`: missing or invalid JWT token
                - `403 Forbidden`: sender belongs to another tenant
                - `404 Not Found`: sender not found
                """)
            .RequireAuthorization()
            .Produces<SenderIngestTokenResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid id,
        ClaimsPrincipal user,
        IWebhookSenderRepository senderRepository,
        IUnitOfWorkFactory uowFactory,
        ILogger<RegenerateSenderIngestTokenEndpoint> logger,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;

        await using var uow = uowFactory.Create();

        var sender = await senderRepository.FindAsync(id, ct);
        if (sender is null) return Results.NotFound();
        if (sender.TenantId != tenantId) return Results.Forbid();

        var rawToken = sender.RotateIngestToken();

        await uow.CommitAsync(ct);

        logger.LogInformation(
            "Sender ingest token rotated. TenantId={TenantId} SenderId={SenderId} ActorEmail={ActorEmail}",
            tenantId, id, user.GetEmail());

        return Results.Ok(new SenderIngestTokenResponse(rawToken));
    }
}
