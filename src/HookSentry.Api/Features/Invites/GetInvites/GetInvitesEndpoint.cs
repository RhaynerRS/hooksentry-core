using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using HookSentry.Api.Common.DTOs;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Invites.Requests;
using HookSentry.Api.DataTransfer.Invites.Responses;
using HookSentry.Domain.Invites;
using NHibernate.Linq;

namespace HookSentry.Api.Features.Invites.GetInvites;

public class GetInvitesEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/invites", Handle)
            .WithName("GetInvites")
            .WithTags("Invites")
            .WithSummary("Lists invites in the authenticated tenant with pagination")
            .WithDescription("""
                Returns a page of invites belonging to the authenticated tenant.
                Only administrators can list invites.

                **Requires authentication with Admin role.**

                **Pagination** *(all optional — have default values):*
                - `Qt`: items per page (default: `10`)
                - `Pg`: page number, 1-based (default: `1`)
                - `CpOrd`: sort field, case-insensitive (default: `id`)
                - `TpOrd`: direction — `Asc` or `Desc` (default: `Desc`)

                **Filters** *(all optional):*
                - `Status`: `0` = Pending, `1` = Used

                **Return codes:**
                - `200 OK`: paginated list of invites
                - `400 Bad Request`: invalid sort field
                - `401 Unauthorized`: missing or invalid token
                - `403 Forbidden`: user does not have Admin role
                """)
            .RequireAuthorization()
            .Produces<PaginationResponse<InviteTokenResponse>>()
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> Handle(
        [AsParameters] GetInvitesRequest request,
        ClaimsPrincipal principal,
        IInviteTokenRepository inviteRepository,
        CancellationToken ct)
    {
        if (principal.RequireAdminRole(out var tenantId) is { } err) return err;

        var baseQuery = inviteRepository.Query().Where(t => t.TenantId == tenantId);

        if (request.Status.HasValue)
            baseQuery = baseQuery.Where(t => t.Status == request.Status.Value);

        var total = await baseQuery.CountAsync(ct);

        List<InviteToken> items;
        try
        {
            items = await ApplyOrdering(baseQuery, request)
                .Skip((request.Pg - 1) * request.Qt)
                .Take(request.Qt)
                .ToListAsync(ct);
        }
        catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }

        return Results.Ok(new PaginationResponse<InviteTokenResponse>(
            total,
            [..items.Select(InviteTokenResponse.From)]));
    }

    private static IQueryable<InviteToken> ApplyOrdering(IQueryable<InviteToken> query, PaginationRequest request)
    {
        var prop = typeof(InviteToken).GetProperty(
            request.CpOrd,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? throw new ArgumentException($"'{request.CpOrd}' is not a valid sort column.");

        var param = Expression.Parameter(typeof(InviteToken), "t");
        var keySelector = Expression.Lambda<Func<InviteToken, object>>(
            Expression.Convert(Expression.Property(param, prop), typeof(object)), param);

        return request.TpOrd == SortOrder.Asc
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);
    }
}
