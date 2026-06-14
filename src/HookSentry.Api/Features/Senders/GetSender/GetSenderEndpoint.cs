using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Senders.Responses;
using HookSentry.Domain.Senders;

namespace HookSentry.Api.Features.Senders.GetSender;

public class GetSenderEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/senders/{id:guid}", Handle)
            .WithName("GetSender")
            .WithTags("Senders")
            .WithSummary("Retorna detalhes de um sender")
            .WithDescription("""
                Retorna os dados de um sender pertencente ao tenant autenticado.

                **Parâmetros de rota:**
                - `id` *(obrigatório)*: UUID do sender

                **Códigos de retorno:**
                - `200 OK`: dados do sender
                - `401 Unauthorized`: token JWT ausente ou inválido
                - `403 Forbidden`: sender pertence a outro tenant
                - `404 Not Found`: sender não encontrado
                """)
            .RequireAuthorization()
            .Produces<SenderResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid id,
        ClaimsPrincipal user,
        IWebhookSenderRepository senderRepository,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;

        var sender = await senderRepository.FindAsync(id, ct);
        if (sender is null) return Results.NotFound();
        if (sender.TenantId != tenantId) return Results.Forbid();

        return Results.Ok(SenderResponse.From(sender));
    }
}
