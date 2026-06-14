using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Events.Responses;
using HookSentry.Domain.Events;

namespace HookSentry.Api.Features.Events.GetEvent;

public class GetEventEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/events/{id:guid}", Handle)
            .WithName("GetEvent")
            .WithTags("Events")
            .WithSummary("Returns event details by ID")
            .WithDescription("""
                Looks up an event by its unique tracking ID.

                **Route parameters:**
                - `id` *(required)*: event UUID

                Returns `403 Forbidden` if the event belongs to another tenant (RNF-007).
                """)
            .RequireAuthorization()
            .Produces<EventResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid id,
        ClaimsPrincipal user,
        IEventRepository eventRepository,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;

        var evt = await eventRepository.FindAsync(id, ct);

        if (evt is null)
            return Results.NotFound();

        if (evt.TenantId != tenantId)
            return Results.Forbid();

        return Results.Ok(EventResponse.From(evt));
    }
}
