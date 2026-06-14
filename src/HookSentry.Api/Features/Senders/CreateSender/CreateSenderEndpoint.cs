using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.Common.Validation;
using HookSentry.Api.DataTransfer.Senders.Requests;
using HookSentry.Api.DataTransfer.Senders.Responses;
using HookSentry.Domain;
using HookSentry.Domain.Destinations;
using HookSentry.Domain.Senders;

namespace HookSentry.Api.Features.Senders.CreateSender;

public class CreateSenderEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/destinations/{destinationId:guid}/senders", Handle)
            .WithName("CreateSender")
            .WithTags("Senders")
            .WithSummary("Registers a sender for a destination URL")
            .WithDescription("""
                Creates a WebhookSender linked to a destination URL. The sender receives its own
                ingest token — use it as the URL in the external service to trigger payload normalization.

                The `ingestToken` returned in the `201` is shown **only once** — save it to configure
                the webhook in the external service. Use `POST /api/v1/senders/{id}/ingest-token` to regenerate.

                **Route parameters:**
                - `destinationId` *(required)*: destination URL UUID

                **Body:**
                - `label` *(optional)*: descriptive name for identification in the dashboard (max 255 characters)

                **Return codes:**
                - `201 Created`: sender created with ingest token
                - `400 Bad Request`: invalid label or control characters
                - `401 Unauthorized`: missing or invalid JWT token
                - `403 Forbidden`: destination URL belongs to another tenant
                - `404 Not Found`: destination URL not found
                """)
            .RequireAuthorization()
            .Produces<CreateSenderResponse>(StatusCodes.Status201Created)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid destinationId,
        CreateSenderRequest request,
        ClaimsPrincipal user,
        IDestinationUrlRepository destinationRepository,
        IWebhookSenderRepository senderRepository,
        IUnitOfWorkFactory uowFactory,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;

        if (request.Label is not null)
        {
            if (InputSanitizer.ValidateName(request.Label) is { } labelErr)
                return Results.BadRequest(labelErr);
        }

        var destination = await destinationRepository.FindAsync(destinationId, ct);
        if (destination is null) return Results.NotFound($"Destination '{destinationId}' not found.");
        if (destination.TenantId != tenantId) return Results.Forbid();

        WebhookSender sender;
        string rawToken;
        try
        {
            sender = new WebhookSender(destinationId, tenantId, request.Label);
            rawToken = sender.RotateIngestToken();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        await using var uow = uowFactory.Create();
        await senderRepository.AddAsync(sender, ct);
        await uow.CommitAsync(ct);

        return Results.Created(
            $"/api/v1/senders/{sender.Id}",
            CreateSenderResponse.From(sender, rawToken));
    }
}
