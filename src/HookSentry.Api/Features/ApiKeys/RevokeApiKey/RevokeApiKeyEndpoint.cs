using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.ApiKeys.Responses;
using HookSentry.Domain;
using HookSentry.Domain.ApiKeys;
using HookSentry.Infrastructure.ApiKeys;

namespace HookSentry.Api.Features.ApiKeys.RevokeApiKey;

public class RevokeApiKeyEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/v1/apikeys/{id:guid}", Handle)
            .WithName("RevokeApiKey")
            .WithTags("API Keys")
            .WithSummary("Revoga uma API key")
            .WithDescription("""
                Revoga imediatamente a API key informada. A chave é invalidada no cache Redis
                e não poderá mais ser usada para autenticação.

                **Parâmetros de rota:**
                - `id` *(obrigatório)*: UUID da API key a revogar

                **Códigos de retorno:**
                - `200 OK`: chave revogada com sucesso
                - `401 Unauthorized`: JWT ausente ou inválido
                - `403 Forbidden`: chave pertence a outro tenant
                - `404 Not Found`: chave não encontrada ou já revogada
                """)
            .RequireAuthorization()
            .Produces<ApiKeyResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid id,
        ClaimsPrincipal user,
        IApiKeyRepository apiKeyRepository,
        IUnitOfWorkFactory uowFactory,
        IApiKeyCacheService cache,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } authErr) return authErr;

        await using var uow = uowFactory.Create();
        var apiKey = await apiKeyRepository.FindAsync(id, ct);

        if (apiKey is null || !apiKey.IsActive) return Results.NotFound();
        if (apiKey.TenantId != tenantId) return Results.Forbid();

        try { apiKey.Revoke(); }
        catch (InvalidOperationException) { return Results.NotFound(); }

        await uow.CommitAsync(ct);
        await cache.RemoveAsync(apiKey.KeyHash, ct);

        return Results.Ok(ApiKeyResponse.From(apiKey));
    }
}
