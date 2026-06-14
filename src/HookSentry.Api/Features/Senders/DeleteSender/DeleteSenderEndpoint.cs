using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Domain;
using HookSentry.Domain.Senders;

namespace HookSentry.Api.Features.Senders.DeleteSender;

public class DeleteSenderEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/v1/senders/{id:guid}", Handle)
            .WithName("DeleteSender")
            .WithTags("Senders")
            .WithSummary("Removes a sender")
            .WithDescription("""
                Removes a sender from the authenticated tenant. The associated ingest token is invalidated immediately.

                **Route parameters:**
                - `id` *(required)*: sender UUID

                **Return codes:**
                - `204 No Content`: sender removed
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

        await senderRepository.RemoveAsync(sender, ct);
        await uow.CommitAsync(ct);

        return Results.NoContent();
    }
}
