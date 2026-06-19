using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Infrastructure.Destinations;
using HookSentry.Domain.Security;
using HookSentry.Api.DataTransfer.Destinations.Requests;
using HookSentry.Api.DataTransfer.Destinations.Responses;
using HookSentry.Domain;
using HookSentry.Domain.Destinations;

namespace HookSentry.Api.Features.Destinations.UpdateDestination;

public class UpdateDestinationEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/v1/destinations/{id:guid}", Handle)
            .WithName("UpdateDestination")
            .WithTags("Destinations")
            .WithSummary("Updates fields of a destination URL")
            .WithDescription("""
                Partially updates a destination URL belonging to the authenticated tenant.

                **Route parameters:**
                - `id` *(required)*: destination URL UUID

                **Body** *(all fields optional):*
                - `url`: new valid HTTPS URL
                - `serverRateLimit`: new concurrent request limit (minimum: 1)
                - `status`: `active` or `inactive` — `suspended` is managed by the Circuit Breaker (RF-011)
                - `authType`: new authentication type — `ApiKey`, `BearerToken`, `JwtBearer`, `BasicAuth`
                - `credentials`: new credentials JSON object (required if authType is provided)
                - `removeAuth`: `true` to remove the configured authentication from the destination

                **Return codes:**
                - `200 OK`: destination URL updated
                - `400 Bad Request`: invalid value, unknown authType, or malformed credentials
                - `401 Unauthorized`: missing or invalid token
                - `403 Forbidden`: destination URL belongs to another tenant (RNF-007)
                - `404 Not Found`: destination URL not found
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
        IDestinationUrlRepository destinationRepository,
        IUnitOfWorkFactory uowFactory,
        ICredentialEncryptionService encryption,
        IDestinationCacheService destinationCache,
        ILogger<UpdateDestinationEndpoint> logger,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;

        await using var uow = uowFactory.Create();

        var destination = await destinationRepository.FindAsync(id, ct);

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
                            "Status 'suspended' is managed by the circuit breaker (RF-011). Use 'active' or 'inactive'.");
                    default:
                        return Results.BadRequest(
                            $"Invalid status '{request.Status}'. Accepted values: 'active', 'inactive'.");
                }
            }

            if (request.RemoveAuth == true)
            {
                destination.SetAuth(null, null);
            }
            else if (request.AuthType is not null || request.Credentials.HasValue)
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

                destination.SetAuth(parsedType, encryption.Encrypt(request.Credentials.Value.GetRawText()));
            }
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        await uow.CommitAsync(ct);

        await destinationCache.RemoveAsync(destination.Id, ct);

        logger.LogInformation(
            "Destination updated. TenantId={TenantId} DestinationId={DestinationId} ActorEmail={ActorEmail}",
            tenantId, id, user.GetEmail());

        return Results.Ok(DestinationResponse.From(destination));
    }
}
