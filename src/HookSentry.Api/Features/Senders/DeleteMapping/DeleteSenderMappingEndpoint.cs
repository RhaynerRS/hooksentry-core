using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Domain;
using HookSentry.Domain.Senders;

namespace HookSentry.Api.Features.Senders.DeleteMapping;

public class DeleteSenderMappingEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/v1/senders/{id:guid}/mapping", Handle)
            .WithName("DeleteSenderMapping")
            .WithTags("Senders")
            .WithSummary("Remove o mapeamento de payload de um sender")
            .WithDescription("""
                Remove o mapeamento configurado para o sender. Após a remoção, eventos ingeridos via
                token deste sender serão enfileirados com o payload bruto, sem transformação.

                **Parâmetros de rota:**
                - `id` *(obrigatório)*: UUID do sender

                **Códigos de retorno:**
                - `204 No Content`: mapeamento removido
                - `401 Unauthorized`: token JWT ausente ou inválido
                - `403 Forbidden`: sender pertence a outro tenant
                - `404 Not Found`: sender não encontrado
                """)
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid id,
        ClaimsPrincipal user,
        IWebhookSenderRepository senderRepository,
        IUnitOfWorkFactory uowFactory,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;

        await using var uow = uowFactory.Create();

        var sender = await senderRepository.FindAsync(id, ct);
        if (sender is null) return Results.NotFound();
        if (sender.TenantId != tenantId) return Results.Forbid();

        sender.SetMapping(null);

        await uow.CommitAsync(ct);

        return Results.NoContent();
    }
}
