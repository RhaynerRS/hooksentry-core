using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Domain;
using HookSentry.Domain.Destinations;
using HookSentry.Domain.Events;
using NHibernate.Linq;

namespace HookSentry.Api.Features.Queues;

public class PurgeQueueEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/v1/queues/{destinationId:guid}", Handle)
            .WithName("PurgeQueue")
            .WithTags("Queues")
            .WithSummary("Cancels all pending messages for a destination (RF-014)")
            .WithDescription("""
                Cancels every pending delivery for a destination by marking its `Pending` and
                `WaitingRetry` events as `Cancelled` in the database. With the single-queue
                architecture, selective RabbitMQ purge by destination is not possible, so the
                purge is a soft cancel — the Worker silently discards messages of cancelled
                events when they arrive.

                Irreversible operation — the frontend must require explicit confirmation before
                calling this endpoint. Events with status `Processing`, `Succeeded`,
                `CriticalFailure` or `AuthenticationFailed` are not affected.

                **Role restriction:** only users with `role = Admin` (or `Owner`) can perform this
                operation. The `role` claim from the JWT is used to verify the role — never the body (RNF-007).

                **Route parameters:**
                - `destinationId` *(required)*: UUID of the destination whose queue is purged

                **Return codes:**
                - `204 No Content`: queue purged successfully
                - `401 Unauthorized`: missing, invalid token, or missing `role` claim
                - `403 Forbidden`: authenticated user is not Admin, or the destination belongs to another tenant (RNF-007)
                - `404 Not Found`: destination not found
                """)
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid destinationId,
        ClaimsPrincipal user,
        IDestinationUrlRepository destinationRepository,
        IEventRepository eventRepository,
        IUnitOfWorkFactory uowFactory,
        ILogger<PurgeQueueEndpoint> logger,
        CancellationToken ct)
    {
        if (user.RequireAdminRole(out var tenantId) is { } err) return err;

        await using var uow = uowFactory.Create();

        var destination = await destinationRepository.FindAsync(destinationId, ct);

        if (destination is null) return Results.NotFound();
        if (destination.TenantId != tenantId) return Results.Forbid();

        var events = await eventRepository.Query()
            .Where(e => e.DestinationUrlId == destinationId
                     && (e.Status == EventStatus.Pending || e.Status == EventStatus.WaitingRetry))
            .ToListAsync(ct);

        foreach (var e in events)
            e.Cancel();

        await uow.CommitAsync(ct);

        logger.LogWarning(
            "Queue purged: {Count} events cancelled for destination {DestinationId} by tenant {TenantId}",
            events.Count, destinationId, tenantId);

        return Results.NoContent();
    }
}
