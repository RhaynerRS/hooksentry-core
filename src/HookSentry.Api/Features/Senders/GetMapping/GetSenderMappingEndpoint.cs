using System.Security.Claims;
using System.Text.Json;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Senders.Responses;
using HookSentry.Domain.Senders;

namespace HookSentry.Api.Features.Senders.GetMapping;

public class GetSenderMappingEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/senders/{id:guid}/mapping", Handle)
            .WithName("GetSenderMapping")
            .WithTags("Senders")
            .WithSummary("Consulta o mapeamento de payload de um sender")
            .WithDescription("""
                Retorna o mapeamento de payload configurado para o sender informado.

                **Parâmetros de rota:**
                - `id` *(obrigatório)*: UUID do sender

                **Códigos de retorno:**
                - `200 OK`: mapeamento configurado
                - `401 Unauthorized`: token JWT ausente ou inválido
                - `403 Forbidden`: sender pertence a outro tenant
                - `404 Not Found`: sender não encontrado ou sem mapeamento configurado
                """)
            .RequireAuthorization()
            .Produces<SenderMappingResponse>()
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
        if (sender.Mapping is null) return Results.NotFound("Nenhum mapeamento configurado para este sender.");

        var mappingElement = JsonSerializer.Deserialize<JsonElement>(sender.Mapping);
        return Results.Ok(new SenderMappingResponse(mappingElement));
    }
}
