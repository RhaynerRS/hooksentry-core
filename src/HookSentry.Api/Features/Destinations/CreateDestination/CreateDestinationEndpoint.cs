using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Domain;
using HookSentry.Domain.Security;
using HookSentry.Api.DataTransfer.Destinations.Requests;
using HookSentry.Api.DataTransfer.Destinations.Responses;
using HookSentry.Domain.Destinations;
using HookSentry.Domain.Tenants;

namespace HookSentry.Api.Features.Destinations.CreateDestination;

public class CreateDestinationEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/destinations", Handle)
            .WithName("CreateDestination")
            .WithTags("Destinations")
            .WithSummary("Registers a new destination URL for the authenticated tenant")
            .WithDescription("""
                Registers an HTTPS destination URL to receive webhooks delivered by HookSentry.

                **Body:**
                - `url` *(required)*: valid HTTPS URL of the destination endpoint
                - `serverRateLimit` *(optional, default: 5)*: maximum number of concurrent requests (RF-006)
                - `authType` *(optional)*: authentication type — `ApiKey`, `BearerToken`, `JwtBearer`, `BasicAuth`
                - `credentials` *(required if authType provided)*: JSON object with credentials (RF-019)

                **`credentials` structure by type:**
                - `ApiKey`: `{ "headerName": "X-Api-Key", "value": "..." }`
                - `BearerToken`: `{ "token": "..." }`
                - `JwtBearer`: `{ "tokenEndpoint": "https://...", "clientId": "...", "clientSecret": "...", "scope": "..." }`
                - `BasicAuth`: `{ "username": "...", "password": "..." }`

                Credentials are encrypted with AES-256-GCM before persisting. They are never returned in responses.

                The `ingestToken` returned in the `201` is shown **only once** — save it to configure
                the webhook in the external service. Use `POST /api/v1/destinations/{id}/ingest-token` to regenerate.

                **Return codes:**
                - `201 Created`: destination URL created
                - `400 Bad Request`: invalid URL, malformed credentials, or unknown authType
                - `401 Unauthorized`: missing or invalid token
                - `404 Not Found`: tenant not found
                """)
            .RequireAuthorization()
            .Produces<CreateDestinationResponse>(StatusCodes.Status201Created)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        CreateDestinationRequest request,
        ClaimsPrincipal user,
        ITenantRepository tenantRepository,
        IDestinationUrlRepository destinationRepository,
        IUnitOfWorkFactory uowFactory,
        ICredentialEncryptionService encryption,
        ILogger<CreateDestinationEndpoint> logger,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;

        var tenant = await tenantRepository.FindAsync(tenantId, ct);
        if (tenant is null)
            return Results.NotFound($"Tenant '{tenantId}' not found.");

        DestinationAuthType? authType = null;
        string? credentialsEncrypted = null;

        if (request.AuthType is not null || request.Credentials.HasValue)
        {
            if (request.AuthType is null)
                return Results.BadRequest("'authType' is required when 'credentials' is provided.");

            if (!request.Credentials.HasValue)
                return Results.BadRequest("'credentials' is required when 'authType' is provided.");

            if (!Enum.TryParse<DestinationAuthType>(request.AuthType, ignoreCase: true, out var parsedType))
                return Results.BadRequest(
                    $"Invalid AuthType '{request.AuthType}'. Accepted values: ApiKey, BearerToken, JwtBearer, BasicAuth.");

            var validationError = CredentialValidator.Validate(parsedType, request.Credentials.Value);
            if (validationError is not null)
                return Results.BadRequest(validationError);

            authType = parsedType;
            credentialsEncrypted = encryption.Encrypt(request.Credentials.Value.GetRawText());
        }

        DestinationUrl destination;
        string rawToken;
        try
        {
            destination = new DestinationUrl(tenantId, request.Url, request.ServerRateLimit);
            destination.SetAuth(authType, credentialsEncrypted);
            rawToken = destination.RotateIngestToken();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        await using var uow = uowFactory.Create();
        await destinationRepository.AddAsync(destination, ct);
        await uow.CommitAsync(ct);

        logger.LogInformation(
            "Destination provisioned. TenantId={TenantId} DestinationId={DestinationId} Url={Url} ActorEmail={ActorEmail}",
            tenantId, destination.Id, request.Url, user.GetEmail());

        return Results.Created(
            $"/api/v1/destinations/{destination.Id}",
            CreateDestinationResponse.From(destination, rawToken));
    }
}
