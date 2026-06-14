using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Destinations.Responses;
using HookSentry.Domain;
using HookSentry.Domain.Destinations;
using HookSentry.Infrastructure.Destinations;

namespace HookSentry.Api.Features.Destinations.RegenerateIngestToken;

public class RegenerateIngestTokenEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/destinations/{id:guid}/ingest-token", Handle)
            .WithName("RegenerateIngestToken")
            .WithTags("Destinations")
            .WithSummary("Regenera o ingest token de uma URL de destino")
            .WithDescription("""
                Gera um novo ingest token para a URL de destino informada, invalidando o anterior.

                O token retornado é exibido **uma única vez** — atualize imediatamente a configuração
                do webhook no serviço externo antes de fechar esta resposta.

                **Parâmetros de rota:**
                - `id` *(obrigatório)*: UUID da URL de destino

                **Códigos de retorno:**
                - `200 OK`: novo ingest token gerado
                - `401 Unauthorized`: token ausente ou inválido
                - `403 Forbidden`: URL de destino pertence a outro tenant
                - `404 Not Found`: URL de destino não encontrada
                """)
            .RequireAuthorization()
            .Produces<IngestTokenResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid id,
        ClaimsPrincipal user,
        IDestinationUrlRepository destinationRepository,
        IUnitOfWorkFactory uowFactory,
        IDestinationCacheService destinationCache,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;

        await using var uow = uowFactory.Create();

        var destination = await destinationRepository.FindAsync(id, ct);
        if (destination is null) return Results.NotFound();
        if (destination.TenantId != tenantId) return Results.Forbid();

        var rawToken = destination.RotateIngestToken();

        await uow.CommitAsync(ct);

        await destinationCache.RemoveAsync(destination.Id, ct);

        return Results.Ok(new IngestTokenResponse(rawToken));
    }
}
