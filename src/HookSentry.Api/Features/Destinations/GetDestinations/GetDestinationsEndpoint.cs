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
            .WithSummary("Lists destination URLs in the authenticated tenant with pagination")
            .WithDescription("""
                Returns a page of destination URLs belonging to the authenticated tenant.

                **Pagination** *(all optional — have default values):*
                - `Qt`: items per page (default: `10`)
                - `Pg`: page number, 1-based (default: `1`)
                - `CpOrd`: sort field, case-insensitive (default: `id`)
                - `TpOrd`: direction — `Asc` or `Desc` (default: `Desc`)

                **Return codes:**
                - `200 OK`: paginated list of destination URLs
                - `400 Bad Request`: invalid sort field
                - `401 Unauthorized`: missing or invalid token
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
