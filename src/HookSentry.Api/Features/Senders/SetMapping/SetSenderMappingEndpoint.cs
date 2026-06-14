using System.Security.Claims;
using System.Text.Json;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Senders.Responses;
using HookSentry.Domain;
using HookSentry.Domain.Senders;

namespace HookSentry.Api.Features.Senders.SetMapping;

public class SetSenderMappingEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/senders/{id:guid}/mapping", Handle)
            .WithName("SetSenderMapping")
            .WithTags("Senders")
            .WithSummary("Cria ou substitui o mapeamento de payload de um sender")
            .WithDescription("""
                Define as regras de transformação de payload para o sender informado. O mapeamento é
                aplicado durante a ingestão quando o `sndr_` token é utilizado.

                O body deve ser um objeto JSON onde cada chave é o nome do campo no payload de saída
                e o valor é uma expressão DSL descrevendo a origem.

                **DSL de mapeamento:**
                - `"campo"` — copia o campo `campo` da raiz
                - `"obj:campo"` — acessa `obj.campo` (`:` é separador de aninhamento)
                - `"array[n]"` — acessa o elemento de índice `n` de um campo array
                - `"a+b"` — soma aritmética (se ambos numéricos) ou concatenação (se string)
                - `["expr1", "expr2"]` — constrói novo array com os valores resolvidos

                **Parâmetros de rota:**
                - `id` *(obrigatório)*: UUID do sender

                **Códigos de retorno:**
                - `200 OK`: mapeamento salvo
                - `400 Bad Request`: body não é um objeto JSON válido
                - `401 Unauthorized`: token JWT ausente ou inválido
                - `403 Forbidden`: sender pertence a outro tenant
                - `404 Not Found`: sender não encontrado
                """)
            .RequireAuthorization()
            .Produces<SenderMappingResponse>()
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid id,
        [Microsoft.AspNetCore.Mvc.FromBody] JsonElement mappingBody,
        ClaimsPrincipal user,
        IWebhookSenderRepository senderRepository,
        IUnitOfWorkFactory uowFactory,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;

        if (mappingBody.ValueKind != JsonValueKind.Object)
            return Results.BadRequest("O mapeamento deve ser um objeto JSON.");

        await using var uow = uowFactory.Create();

        var sender = await senderRepository.FindAsync(id, ct);
        if (sender is null) return Results.NotFound();
        if (sender.Mapping != null)
            return Results.BadRequest("O mapeamento já existe para este sender.");
        if (sender.TenantId != tenantId) return Results.Forbid();

        sender.SetMapping(mappingBody.GetRawText());

        await uow.CommitAsync(ct);

        return Results.Ok(new SenderMappingResponse(mappingBody));
    }
}
