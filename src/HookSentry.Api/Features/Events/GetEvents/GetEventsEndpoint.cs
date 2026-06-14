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
            .WithSummary("Lista eventos do tenant autenticado com paginação e filtros")
            .WithDescription("""
                Retorna uma página de eventos pertencentes ao tenant autenticado.

                **Paginação** *(todos opcionais — possuem valores padrão):*
                - `Qt`: itens por página (padrão: `10`)
                - `Pg`: número da página, base 1 (padrão: `1`)
                - `CpOrd`: campo de ordenação (padrão: `acceptedAt`)
                - `TpOrd`: direção — `Asc` ou `Desc` (padrão: `Desc`)

                **Filtros** *(todos opcionais):*
                - `Status`: filtra por status do evento — `Pending`, `Processing`, `Succeeded`,
                  `Failed`, `WaitingRetry`, `CriticalFailure`, `Cancelled`
                - `DestinationUrlId`: filtra por UUID da URL de destino
                - `From`: filtra eventos aceitos a partir desta data/hora (ISO 8601)
                - `To`: filtra eventos aceitos até esta data/hora (ISO 8601)
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
                return Results.BadRequest($"Status inválido: '{request.Status}'.");
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
