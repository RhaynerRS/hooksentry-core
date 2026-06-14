using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using HookSentry.Api.Common.DTOs;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Events.Requests;
using HookSentry.Api.DataTransfer.Events.Responses;
using HookSentry.Domain.Events;
using NHibernate.Linq;

namespace HookSentry.Api.Features.Events.GetEvents;

public class GetEventsEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/events", Handle)
            .WithName("GetEvents")
            .WithTags("Events")
            .WithSummary("Lists events in the authenticated tenant with pagination and filters")
            .WithDescription("""
                Returns a page of events belonging to the authenticated tenant.

                **Pagination** *(all optional — have default values):*
                - `Qt`: items per page (default: `10`)
                - `Pg`: page number, 1-based (default: `1`)
                - `CpOrd`: sort field (default: `acceptedAt`)
                - `TpOrd`: direction — `Asc` or `Desc` (default: `Desc`)

                **Filters** *(all optional):*
                - `Status`: filter by event status — `Pending`, `Processing`, `Succeeded`,
                  `Failed`, `WaitingRetry`, `CriticalFailure`, `Cancelled`
                - `DestinationUrlId`: filter by destination URL UUID
                - `From`: filter events accepted from this date/time (ISO 8601)
                - `To`: filter events accepted up to this date/time (ISO 8601)
                """)
            .RequireAuthorization()
            .Produces<PaginationResponse<EventResponse>>()
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> Handle(
        [AsParameters] GetEventsRequest request,
        ClaimsPrincipal user,
        IEventRepository eventRepository,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;

        var baseQuery = eventRepository.Query().Where(e => e.TenantId == tenantId);

        if (request.Status is not null)
        {
            if (!Enum.TryParse<EventStatus>(request.Status, ignoreCase: true, out var parsedStatus))
                return Results.BadRequest($"Invalid status: '{request.Status}'.");
            baseQuery = baseQuery.Where(e => e.Status == parsedStatus);
        }

        if (request.DestinationUrlId.HasValue)
            baseQuery = baseQuery.Where(e => e.DestinationUrlId == request.DestinationUrlId.Value);

        if (request.From.HasValue)
            baseQuery = baseQuery.Where(e => e.AcceptedAt >= request.From.Value);

        if (request.To.HasValue)
            baseQuery = baseQuery.Where(e => e.AcceptedAt <= request.To.Value);

        var total = await baseQuery.CountAsync(ct);

        List<Event> items;
        try
        {
            items = await ApplyOrdering(baseQuery, request)
                .Skip((request.Pg - 1) * request.Qt)
                .Take(request.Qt)
                .ToListAsync(ct);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        return Results.Ok(new PaginationResponse<EventResponse>(
            total,
            [..items.Select(EventResponse.From)]));
    }

    private static IQueryable<Event> ApplyOrdering(IQueryable<Event> query, PaginationRequest request)
    {
        var prop = typeof(Event).GetProperty(
            request.CpOrd,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? throw new ArgumentException($"'{request.CpOrd}' is not a valid sort column.");

        var param = Expression.Parameter(typeof(Event), "e");
        var keySelector = Expression.Lambda<Func<Event, object>>(
            Expression.Convert(Expression.Property(param, prop), typeof(object)),
            param);

        return request.TpOrd == SortOrder.Asc
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);
    }
}
