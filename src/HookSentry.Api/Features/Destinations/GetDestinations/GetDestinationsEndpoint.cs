using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using HookSentry.Api.Common.DTOs;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Destinations.Requests;
using HookSentry.Api.DataTransfer.Destinations.Responses;
using HookSentry.Domain.Destinations;
using NHibernate.Linq;

namespace HookSentry.Api.Features.Destinations.GetDestinations;

public class GetDestinationsEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/destinations", Handle)
            .WithName("GetDestinations")
            .WithTags("Destinations")
            .WithSummary("Lista URLs de destino do tenant autenticado com paginação")
            .WithDescription("""
                Retorna uma página de URLs de destino pertencentes ao tenant autenticado.

                **Paginação** *(todos opcionais — possuem valores padrão):*
                - `Qt`: itens por página (padrão: `10`)
                - `Pg`: número da página, base 1 (padrão: `1`)
                - `CpOrd`: campo de ordenação, case-insensitive (padrão: `id`)
                - `TpOrd`: direção — `Asc` ou `Desc` (padrão: `Desc`)

                **Códigos de retorno:**
                - `200 OK`: lista paginada de URLs de destino
                - `400 Bad Request`: campo de ordenação inválido
                - `401 Unauthorized`: token ausente ou inválido
                """)
            .RequireAuthorization()
            .Produces<PaginationResponse<DestinationResponse>>()
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> Handle(
        [AsParameters] GetDestinationsRequest request,
        ClaimsPrincipal user,
        IDestinationUrlRepository destinationRepository,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;

        var baseQuery = destinationRepository.Query().Where(d => d.TenantId == tenantId);

        var total = await baseQuery.CountAsync(ct);

        List<DestinationUrl> items;
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

        return Results.Ok(new PaginationResponse<DestinationResponse>(
            total,
            [..items.Select(DestinationResponse.From)]));
    }

    private static IQueryable<DestinationUrl> ApplyOrdering(
        IQueryable<DestinationUrl> query, PaginationRequest request)
    {
        var prop = typeof(DestinationUrl).GetProperty(
            request.CpOrd,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? throw new ArgumentException($"'{request.CpOrd}' is not a valid sort column.");

        var param = Expression.Parameter(typeof(DestinationUrl), "d");
        var keySelector = Expression.Lambda<Func<DestinationUrl, object>>(
            Expression.Convert(Expression.Property(param, prop), typeof(object)),
            param);

        return request.TpOrd == SortOrder.Asc
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);
    }
}
