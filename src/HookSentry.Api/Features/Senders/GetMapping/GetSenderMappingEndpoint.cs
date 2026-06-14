using System.Security.Claims;
using System.Text.Json;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Senders.Responses;
using HookSentry.Domain.Senders;

namespace HookSentry.Api.Features.Senders.GetMapping;

public class GetSenderMappingEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/senders/{id:guid}/mapping", Handle)
            .WithName("GetSenderMapping")
            .WithTags("Senders")
            .WithSummary("Retrieves the payload mapping of a sender")
            .WithDescription("""
                Returns the configured payload mapping for the specified sender.

                **Route parameters:**
                - `id` *(required)*: sender UUID

                **Return codes:**
                - `200 OK`: configured mapping
                - `401 Unauthorized`: missing or invalid JWT token
                - `403 Forbidden`: sender belongs to another tenant
                - `404 Not Found`: sender not found or no mapping configured
                """)
            .RequireAuthorization()
            .Produces<SenderMappingResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid id,
        ClaimsPrincipal user,
        IWebhookSenderRepository senderRepository,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;

        var sender = await senderRepository.FindAsync(id, ct);
        if (sender is null) return Results.NotFound();
        if (sender.TenantId != tenantId) return Results.Forbid();
        if (sender.Mapping is null) return Results.NotFound("No mapping configured for this sender.");

        var mappingElement = JsonSerializer.Deserialize<JsonElement>(sender.Mapping);
        return Results.Ok(new SenderMappingResponse(mappingElement));
    }
}
