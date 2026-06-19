using System.Security.Claims;
using System.Text.Json;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Senders.Responses;
using HookSentry.Domain;
using HookSentry.Domain.Senders;

namespace HookSentry.Api.Features.Senders.SetMapping;

public class SetSenderMappingEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/senders/{id:guid}/mapping", Handle)
            .WithName("SetSenderMapping")
            .WithTags("Senders")
            .WithSummary("Creates or replaces the payload mapping of a sender")
            .WithDescription("""
                Defines payload transformation rules for the specified sender. The mapping is
                applied during ingestion when the `sndr_` token is used.

                The body must be a JSON object where each key is the field name in the output payload
                and the value is a DSL expression describing the source.

                **Mapping DSL:**
                - `"field"` — copies the `field` field from the root
                - `"obj:field"` — accesses `obj.field` (`:` is the nesting separator)
                - `"array[n]"` — accesses the element at index `n` of an array field
                - `"a+b"` — arithmetic sum (if both numeric) or concatenation (if string)
                - `["expr1", "expr2"]` — builds a new array with the resolved values

                **Route parameters:**
                - `id` *(required)*: sender UUID

                **Return codes:**
                - `200 OK`: mapping saved
                - `400 Bad Request`: body is not a valid JSON object
                - `401 Unauthorized`: missing or invalid JWT token
                - `403 Forbidden`: sender belongs to another tenant
                - `404 Not Found`: sender not found
                """)
            .RequireAuthorization()
            .Produces<SenderMappingResponse>()
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid id,
        [Microsoft.AspNetCore.Mvc.FromBody] JsonElement mappingBody,
        ClaimsPrincipal user,
        IWebhookSenderRepository senderRepository,
        IUnitOfWorkFactory uowFactory,
        ILogger<SetSenderMappingEndpoint> logger,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;

        if (mappingBody.ValueKind != JsonValueKind.Object)
            return Results.BadRequest("The mapping must be a JSON object.");

        await using var uow = uowFactory.Create();

        var sender = await senderRepository.FindAsync(id, ct);
        if (sender is null) return Results.NotFound();
        if (sender.Mapping != null)
            return Results.BadRequest("A mapping already exists for this sender.");
        if (sender.TenantId != tenantId) return Results.Forbid();

        sender.SetMapping(mappingBody.GetRawText());

        await uow.CommitAsync(ct);

        logger.LogInformation(
            "Sender mapping set. TenantId={TenantId} SenderId={SenderId} ActorEmail={ActorEmail}",
            tenantId, id, user.GetEmail());

        return Results.Ok(new SenderMappingResponse(mappingBody));
    }
}
