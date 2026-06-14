using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using HookSentry.Api.Common.DTOs;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Senders.Requests;
using HookSentry.Api.DataTransfer.Senders.Responses;
using HookSentry.Domain.Destinations;
using HookSentry.Domain.Senders;
using NHibernate.Linq;

namespace HookSentry.Api.Features.Senders.GetSenders;

public class GetSendersEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/destinations/{destinationId:guid}/senders", Handle)
            .WithName("GetSenders")
            .WithTags("Senders")
            .WithSummary("Lists senders of a destination URL with pagination")
            .WithDescription("""
                Returns a page of senders linked to the specified destination URL.

                **Route parameters:**
                - `destinationId` *(required)*: destination URL UUID

                **Pagination** *(all optional — have default values):*
                - `Qt`: items per page (default: `10`)
                - `Pg`: page number, 1-based (default: `1`)
                - `CpOrd`: sort field, case-insensitive (default: `id`)
                - `TpOrd`: direction — `Asc` or `Desc` (default: `Desc`)

                **Return codes:**
                - `200 OK`: paginated list of senders
                - `400 Bad Request`: invalid sort field
                - `401 Unauthorized`: missing or invalid JWT token
                - `403 Forbidden`: destination URL belongs to another tenant
                - `404 Not Found`: destination URL not found
                """)
            .RequireAuthorization()
            .Produces<PaginationResponse<SenderResponse>>()
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid destinationId,
        [AsParameters] GetSendersRequest request,
        ClaimsPrincipal user,
        IDestinationUrlRepository destinationRepository,
        IWebhookSenderRepository senderRepository,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;

        var destination = await destinationRepository.FindAsync(destinationId, ct);
        if (destination is null) return Results.NotFound($"Destination '{destinationId}' not found.");
        if (destination.TenantId != tenantId) return Results.Forbid();

        var baseQuery = senderRepository.Query().Where(s => s.DestinationId == destinationId);

        var total = await baseQuery.CountAsync(ct);

        List<WebhookSender> items;
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

        return Results.Ok(new PaginationResponse<SenderResponse>(
            total,
            [..items.Select(SenderResponse.From)]));
    }

    private static IQueryable<WebhookSender> ApplyOrdering(
        IQueryable<WebhookSender> query, PaginationRequest request)
    {
        var prop = typeof(WebhookSender).GetProperty(
            request.CpOrd,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? throw new ArgumentException($"'{request.CpOrd}' is not a valid sort column.");

        var param = Expression.Parameter(typeof(WebhookSender), "s");
        var keySelector = Expression.Lambda<Func<WebhookSender, object>>(
            Expression.Convert(Expression.Property(param, prop), typeof(object)),
            param);

        return request.TpOrd == SortOrder.Asc
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);
    }
}
