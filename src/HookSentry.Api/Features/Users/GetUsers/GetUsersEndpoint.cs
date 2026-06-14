using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using HookSentry.Api.Common.DTOs;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Users.Requests;
using HookSentry.Api.DataTransfer.Users.Responses;
using HookSentry.Domain.Users;
using NHibernate.Linq;

namespace HookSentry.Api.Features.Users.GetUsers;

public class GetUsersEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/users", Handle)
            .WithName("GetUsers")
            .WithTags("Users")
            .WithSummary("Lists users in the authenticated tenant with pagination")
            .WithDescription("""
                Returns a page of users belonging to the authenticated tenant (RNF-007).
                The listing is always isolated by tenant — never exposes users from other tenants.

                **Pagination** *(all optional — have default values):*
                - `Qt`: items per page (default: `10`)
                - `Pg`: page number, 1-based (default: `1`)
                - `CpOrd`: sort field, case-insensitive (default: `id`)
                - `TpOrd`: direction — `0` = Asc, `1` = Desc (default: `1`)

                **Filters** *(all optional):*
                - `Status`: filter by status — `0` = Active, `1` = Inactive
                - `Role`: filter by role — `0` = Developer, `1` = Admin

                **Return codes:**
                - `200 OK`: paginated list of users (without password field)
                - `400 Bad Request`: invalid sort field
                - `401 Unauthorized`: missing or invalid token
                """)
            .RequireAuthorization()
            .Produces<PaginationResponse<UserResponse>>()
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> Handle(
        [AsParameters] GetUsersRequest request,
        ClaimsPrincipal principal,
        IUserRepository userRepository,
        CancellationToken ct)
    {
        if (principal.RequireTenantId(out var tenantId) is { } err) return err;

        var query = userRepository.Query().Where(u => u.TenantId == tenantId);

        if (request.Status.HasValue)
            query = query.Where(u => u.Status == request.Status.Value);

        if (request.Role.HasValue)
            query = query.Where(u => u.Role == request.Role.Value);

        var total = await query.CountAsync(ct);

        List<User> items;
        try
        {
            items = await ApplyOrdering(query, request)
                .Skip((request.Pg - 1) * request.Qt)
                .Take(request.Qt)
                .ToListAsync(ct);
        }
        catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }

        return Results.Ok(new PaginationResponse<UserResponse>(
            total,
            [..items.Select(UserResponse.From)]));
    }

    private static IQueryable<User> ApplyOrdering(IQueryable<User> query, PaginationRequest request)
    {
        var prop = typeof(User).GetProperty(
            request.CpOrd,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? throw new ArgumentException($"'{request.CpOrd}' is not a valid sort column.");

        var param = Expression.Parameter(typeof(User), "u");
        var keySelector = Expression.Lambda<Func<User, object>>(
            Expression.Convert(Expression.Property(param, prop), typeof(object)), param);

        return request.TpOrd == SortOrder.Asc
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);
    }
}
