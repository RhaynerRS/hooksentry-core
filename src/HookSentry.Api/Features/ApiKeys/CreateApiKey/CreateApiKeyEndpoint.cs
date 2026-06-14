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
            .WithSummary("Creates a new API key")
            .WithDescription("""
                Generates a new API key for the authenticated tenant. The plain text value is returned
                **only in this response** — store it securely, as it cannot be retrieved again.

                The key must be sent in the `X-Api-Key` header on requests to the ingest endpoint.

                **Body:**
                - `name` *(required)*: descriptive key name (max 100 characters)

                **Return codes:**
                - `201 Created`: key created successfully — contains the plain text value
                - `400 Bad Request`: invalid name
                - `401 Unauthorized`: missing or invalid JWT
                - `404 Not Found`: tenant not found
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
