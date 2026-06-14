using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Domain;
using HookSentry.Domain.Senders;

namespace HookSentry.Api.Features.Senders.DeleteMapping;

public class DeleteSenderMappingEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/v1/senders/{id:guid}/mapping", Handle)
            .WithName("DeleteSenderMapping")
            .WithTags("Senders")
            .WithSummary("Removes the payload mapping of a sender")
            .WithDescription("""
                Removes the configured mapping for the sender. After removal, events ingested via
                this sender's token will be queued with the raw payload, without transformation.

                **Route parameters:**
                - `id` *(required)*: sender UUID

                **Return codes:**
                - `204 No Content`: mapping removed
                - `401 Unauthorized`: missing or invalid JWT token
                - `403 Forbidden`: sender belongs to another tenant
                - `404 Not Found`: sender not found
                """)
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid id,
        ClaimsPrincipal user,
        IWebhookSenderRepository senderRepository,
        IUnitOfWorkFactory uowFactory,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;

        await using var uow = uowFactory.Create();

        var sender = await senderRepository.FindAsync(id, ct);
        if (sender is null) return Results.NotFound();
        if (sender.TenantId != tenantId) return Results.Forbid();

        sender.SetMapping(null);

        await uow.CommitAsync(ct);

        return Results.NoContent();
    }
}
