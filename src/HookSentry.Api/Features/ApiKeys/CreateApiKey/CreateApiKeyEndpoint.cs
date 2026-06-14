using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.Common.Validation;
using HookSentry.Api.DataTransfer.ApiKeys.Requests;
using HookSentry.Api.DataTransfer.ApiKeys.Responses;
using HookSentry.Domain;
using HookSentry.Domain.ApiKeys;
using HookSentry.Infrastructure.ApiKeys;

namespace HookSentry.Api.Features.ApiKeys.CreateApiKey;

public class CreateApiKeyEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/apikeys", Handle)
            .WithName("CreateApiKey")
            .WithTags("API Keys")
            .WithSummary("Cria uma nova API key")
            .WithDescription("""
                Gera uma nova API key para o tenant autenticado. O valor em texto claro é retornado
                **apenas nesta resposta** — armazene-o com segurança, pois não pode ser recuperado.

                A chave deve ser enviada no header `X-Api-Key` nas requisições ao endpoint de ingest.

                **Body:**
                - `name` *(obrigatório)*: nome descritivo da chave (máx. 100 caracteres)

                **Códigos de retorno:**
                - `201 Created`: chave criada com sucesso — contém o valor em texto claro
                - `400 Bad Request`: nome inválido
                - `401 Unauthorized`: JWT ausente ou inválido
                - `404 Not Found`: tenant não encontrado
                """)
            .RequireAuthorization()
            .Produces<ApiKeyCreatedResponse>(StatusCodes.Status201Created)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        CreateApiKeyRequest request,
        ClaimsPrincipal user,
        IApiKeyRepository apiKeyRepository,
        IUnitOfWorkFactory uowFactory,
        IApiKeyCacheService cache,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } authErr) return authErr;

        if (InputSanitizer.ValidateName(request.Name) is { } nameErr)
            return Results.BadRequest(nameErr);

        ApiKey apiKey;
        try { apiKey = new ApiKey(tenantId, request.Name); }
        catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }

        await using var uow = uowFactory.Create();
        await apiKeyRepository.AddAsync(apiKey, ct);
        await uow.CommitAsync(ct);

        await cache.SetAsync(apiKey.KeyHash, new ApiKeyCacheEntry(tenantId), ct);

        return Results.Created(
            $"/api/v1/apikeys/{apiKey.Id}",
            new ApiKeyCreatedResponse(apiKey.Id, apiKey.Name, apiKey.RawKey!, apiKey.CreatedAt));
    }
}
