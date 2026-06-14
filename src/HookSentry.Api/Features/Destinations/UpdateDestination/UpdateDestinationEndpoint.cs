using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Infrastructure.Destinations;
using HookSentry.Infrastructure.Security;
using HookSentry.Api.DataTransfer.Destinations.Requests;
using HookSentry.Api.DataTransfer.Destinations.Responses;
using HookSentry.Domain.Destinations;

namespace HookSentry.Api.Features.Destinations.UpdateDestination;

public class UpdateDestinationEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/v1/destinations/{id:guid}", Handle)
            .WithName("UpdateDestination")
            .WithTags("Destinations")
            .WithSummary("Atualiza campos de uma URL de destino")
            .WithDescription("""
                Atualiza parcialmente uma URL de destino pertencente ao tenant autenticado.

                **Parâmetros de rota:**
                - `id` *(obrigatório)*: UUID da URL de destino

                **Body** *(todos os campos são opcionais):*
                - `url`: nova URL HTTPS válida
                - `serverRateLimit`: novo limite de requisições simultâneas (mínimo: 1)
                - `status`: `active` ou `inactive` — `suspended` é gerenciado pelo Circuit Breaker (RF-011)
                - `authType`: novo tipo de autenticação — `ApiKey`, `BearerToken`, `JwtBearer`, `BasicAuth`
                - `credentials`: novo objeto JSON de credenciais (obrigatório se authType for informado)
                - `removeAuth`: `true` para remover a autenticação configurada no destino

                **Códigos de retorno:**
                - `200 OK`: URL de destino atualizada
                - `400 Bad Request`: valor inválido, authType desconhecido ou credenciais malformadas
                - `401 Unauthorized`: token ausente ou inválido
                - `403 Forbidden`: URL de destino pertence a outro tenant (RNF-007)
                - `404 Not Found`: URL de destino não encontrada
                """)
            .RequireAuthorization()
            .Produces<DestinationResponse>()
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid id,
        UpdateDestinationRequest request,
        ClaimsPrincipal user,
        NHibernate.ISession session,
        ICredentialEncryptionService encryption,
        IDestinationCacheService destinationCache,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;

        using var tx = session.BeginTransaction();

        var destination = await session.GetAsync<DestinationUrl>(id, ct);

        if (destination is null)
            return Results.NotFound();

        if (destination.TenantId != tenantId)
            return Results.Forbid();

        try
        {
            if (request.Url is not null)
                destination.SetUrl(request.Url);

            if (request.ServerRateLimit.HasValue)
                destination.SetServerRateLimit(request.ServerRateLimit.Value);

            if (request.Status is not null)
            {
                switch (request.Status.ToLowerInvariant())
                {
                    case "active":
                        destination.Activate();
                        break;
                    case "inactive":
                        destination.Deactivate();
                        break;
                    case "suspended":
                        return Results.BadRequest(
                            "Status 'suspended' é gerenciado pelo circuit breaker (RF-011). Use 'active' ou 'inactive'.");
                    default:
                        return Results.BadRequest(
                            $"Status '{request.Status}' inválido. Valores aceitos: 'active', 'inactive'.");
                }
            }

            if (request.RemoveAuth == true)
            {
                destination.SetAuth(null, null);
            }
            else if (request.AuthType is not null || request.Credentials.HasValue)
            {
                if (request.AuthType is null)
                    return Results.BadRequest("'authType' é obrigatório quando 'credentials' é informado.");

                if (!request.Credentials.HasValue)
                    return Results.BadRequest("'credentials' é obrigatório quando 'authType' é informado.");

                if (!Enum.TryParse<DestinationAuthType>(request.AuthType, ignoreCase: true, out var parsedType))
                    return Results.BadRequest(
                        $"AuthType '{request.AuthType}' inválido. Valores aceitos: ApiKey, BearerToken, JwtBearer, BasicAuth.");

                var validationError = CredentialValidator.Validate(parsedType, request.Credentials.Value);
                if (validationError is not null)
                    return Results.BadRequest(validationError);

                destination.SetAuth(parsedType, encryption.Encrypt(request.Credentials.Value.GetRawText()));
            }
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        await tx.CommitAsync(ct);

        await destinationCache.RemoveAsync(destination.Id, ct);

        return Results.Ok(DestinationResponse.From(destination));
    }
}
