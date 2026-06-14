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
            .WithSummary("Regenerates the ingest token of a destination URL")
            .WithDescription("""
                Generates a new ingest token for the specified destination URL, invalidating the previous one.

                The returned token is shown **only once** — immediately update the webhook configuration
                in the external service before closing this response.

                **Route parameters:**
                - `id` *(required)*: destination URL UUID

                **Return codes:**
                - `200 OK`: new ingest token generated
                - `401 Unauthorized`: missing or invalid token
                - `403 Forbidden`: destination URL belongs to another tenant
                - `404 Not Found`: destination URL not found
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
