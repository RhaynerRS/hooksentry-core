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
            .WithSummary("Revokes an API key")
            .WithDescription("""
                Immediately revokes the specified API key. The key is invalidated in the Redis cache
                and can no longer be used for authentication.

                **Route parameters:**
                - `id` *(required)*: UUID of the API key to revoke

                **Return codes:**
                - `200 OK`: key revoked successfully
                - `401 Unauthorized`: missing or invalid JWT
                - `403 Forbidden`: key belongs to another tenant
                - `404 Not Found`: key not found or already revoked
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
        ILogger<RevokeApiKeyEndpoint> logger,
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

        logger.LogInformation(
            "ApiKey revoked. TenantId={TenantId} ApiKeyId={ApiKeyId} ActorEmail={ActorEmail}",
            tenantId, id, user.GetEmail());

        return Results.Ok(ApiKeyResponse.From(apiKey));
    }
}
