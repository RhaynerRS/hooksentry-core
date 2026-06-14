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
            .WithSummary("Lista senders de uma URL de destino com paginação")
            .WithDescription("""
                Retorna uma página de senders vinculados à URL de destino informada.

                **Parâmetros de rota:**
                - `destinationId` *(obrigatório)*: UUID da URL de destino

                **Paginação** *(todos opcionais — possuem valores padrão):*
                - `Qt`: itens por página (padrão: `10`)
                - `Pg`: número da página, base 1 (padrão: `1`)
                - `CpOrd`: campo de ordenação, case-insensitive (padrão: `id`)
                - `TpOrd`: direção — `Asc` ou `Desc` (padrão: `Desc`)

                **Códigos de retorno:**
                - `200 OK`: lista paginada de senders
                - `400 Bad Request`: campo de ordenação inválido
                - `401 Unauthorized`: token JWT ausente ou inválido
                - `403 Forbidden`: URL de destino pertence a outro tenant
                - `404 Not Found`: URL de destino não encontrada
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
