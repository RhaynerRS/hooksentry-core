using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Events.Responses;
using HookSentry.Domain;
using HookSentry.Domain.Destinations;
using HookSentry.Domain.Events;
using HookSentry.Domain.Tenants;
using HookSentry.Infrastructure.RabbitMq;

namespace HookSentry.Api.Features.Events.ReplayEvent;

public class ReplayEventEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/events/{id:guid}/replay", Handle)
            .WithName("ReplayEvent")
            .WithTags("Events")
            .WithSummary("Manually replays an event with CriticalFailure status")
            .WithDescription("""
                Reprocesses an event that exhausted all automatic retry attempts (RF-013).

                **Route parameters:**
                - `id` *(required)*: UUID of the event to replay

                **Precondition:** the event must have `CriticalFailure` status.
                Any other status returns `400 Bad Request`.

                **Effects when replaying:**
                - `currentRetryCount` is reset to zero
                - `nextAttemptAt` is set to `now()`
                - `status` is changed to `Pending`

                Returns `403 Forbidden` if the event belongs to another tenant (RNF-007).
                """)
            .RequireAuthorization()
            .Produces<EventResponse>()
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid id,
        ClaimsPrincipal user,
        IEventRepository eventRepository,
        IDestinationUrlRepository destinationRepository,
        ITenantRepository tenantRepository,
        IUnitOfWorkFactory uowFactory,
        IEventPublisher publisher,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;

        await using var uow = uowFactory.Create();

        var evt = await eventRepository.FindAsync(id, ct);

        if (evt is null)
            return Results.NotFound();

        if (evt.TenantId != tenantId)
            return Results.Forbid();

        var destination = await destinationRepository.FindAsync(evt.DestinationUrlId, ct);
        var tenant = await tenantRepository.FindAsync(evt.TenantId, ct);

        if (destination is null || tenant is null)
            return Results.Problem("Destination or tenant data not found.");

        try { evt.ResetForReplay(); }
        catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }

        await uow.CommitAsync(ct);

        await publisher.PublishAsync(new EventMessage(
            EventId: evt.Id,
            TenantId: evt.TenantId,
            DestinationUrlId: evt.DestinationUrlId,
            DestinationUrl: destination.Url,
            Payload: evt.Payload,
            RetryCount: 0,
            MaxTrys: tenant.MaxTrys,
            WebhookSecret: tenant.WebhookSecret,
            ServerRateLimit: destination.ServerRateLimit,
            AuthType: destination.AuthType,
            CredentialsEncrypted: destination.CredentialsEncrypted
        ), ct);

        return Results.Ok(EventResponse.From(evt));
    }
}
