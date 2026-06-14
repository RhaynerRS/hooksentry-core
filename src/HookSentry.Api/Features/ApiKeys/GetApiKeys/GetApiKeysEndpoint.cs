using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using HookSentry.Api.Common.DTOs;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.ApiKeys.Requests;
using HookSentry.Api.DataTransfer.ApiKeys.Responses;
using HookSentry.Domain.ApiKeys;
using NHibernate.Linq;

namespace HookSentry.Api.Features.ApiKeys.GetApiKeys;

public class GetApiKeysEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/apikeys", Handle)
            .WithName("GetApiKeys")
            .WithTags("API Keys")
            .WithSummary("Lists API keys of the authenticated tenant")
            .WithDescription("""
                Returns a page of API keys belonging to the authenticated tenant.

                **Pagination** *(all optional — have default values):*
                - `Qt`: items per page (default: `10`)
                - `Pg`: page number, 1-based (default: `1`)
                - `CpOrd`: sort field, case-insensitive (default: `id`)
                - `TpOrd`: direction — `Asc` or `Desc` (default: `Desc`)

                **Filters** *(all optional):*
                - `IsActive`: `true` for active, `false` for revoked

                **Return codes:**
                - `200 OK`: paginated list of API keys
                - `401 Unauthorized`: missing or invalid JWT
                """)
            .RequireAuthorization()
            .Produces<PaginationResponse<ApiKeyResponse>>()
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> Handle(
        [AsParameters] GetApiKeysRequest request,
        ClaimsPrincipal user,
        IApiKeyRepository apiKeyRepository,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } authErr) return authErr;

        var baseQuery = apiKeyRepository.Query().Where(k => k.TenantId == tenantId);

        if (request.IsActive.HasValue)
            baseQuery = baseQuery.Where(k => k.IsActive == request.IsActive.Value);

        var total = await baseQuery.CountAsync(ct);

        List<ApiKey> items;
        try
        {
            items = await ApplyOrdering(baseQuery, request)
                .Skip((request.Pg - 1) * request.Qt)
                .Take(request.Qt)
                .ToListAsync(ct);
        }
        catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }

        return Results.Ok(new PaginationResponse<ApiKeyResponse>(
            total,
            [..items.Select(ApiKeyResponse.From)]));
    }

    private static IQueryable<ApiKey> ApplyOrdering(IQueryable<ApiKey> query, PaginationRequest request)
    {
        var prop = typeof(ApiKey).GetProperty(
            request.CpOrd,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? throw new ArgumentException($"'{request.CpOrd}' is not a valid sort column.");

        var param = Expression.Parameter(typeof(ApiKey), "k");
        var keySelector = Expression.Lambda<Func<ApiKey, object>>(
            Expression.Convert(Expression.Property(param, prop), typeof(object)), param);

        return request.TpOrd == SortOrder.Asc
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);
    }
}
