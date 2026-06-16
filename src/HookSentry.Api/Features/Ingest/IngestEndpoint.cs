using System.Security.Claims;
using System.Text.Json;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.Common.Validation;
using HookSentry.Api.DataTransfer.Events.Responses;
using HookSentry.Domain;
using HookSentry.Domain.Common;
using HookSentry.Domain.Destinations;
using HookSentry.Domain.Events;
using HookSentry.Domain.Senders;
using HookSentry.Domain.Tenants;
using HookSentry.Infrastructure.Destinations;
using HookSentry.Infrastructure.Events;
using HookSentry.Infrastructure.RabbitMq;

namespace HookSentry.Api.Features.Ingest;

public class IngestEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/ingest/{tenantId:guid}/{token}", Handle)
            .WithName("IngestEvent")
            .WithTags("Ingest")
            .WithSummary("Ingests an event via ingest token")
            .WithDescription("""
                Receives an arbitrary JSON payload and persists it for asynchronous delivery to the destination URL
                associated with the provided ingest token.

                **Two token types are accepted:**
                - `dst_<token>` — destination URL token; payload delivered without transformation
                - `sndr_<token>` — sender token; payload transformed by the configured mapping (if any)

                Configure the URL `https://{host}/api/v1/ingest/{tenantId}/{token}` directly in the external service
                that emits webhooks — no need to structure the payload.

                **Route parameters:**
                - `tenantId` *(required)*: tenant UUID
                - `token` *(required)*: ingest token of the destination URL or sender

                **Headers:**
                - `X-Api-Key` *(required)*: API key for authentication
                - `X-Idempotency-Key` *(optional)*: key of up to 255 characters — if an event already exists
                  with the same key for this tenant, returns `200 OK` with the original event data
                  without reprocessing

                **Body:**
                - Arbitrary JSON object to be delivered to the destination URL

                **Return codes:**
                - `202 Accepted`: event accepted for asynchronous delivery
                - `200 OK`: duplicate event returned via idempotency key
                - `400 Bad Request`: invalid payload or token with invalid format
                - `401 Unauthorized`: missing or invalid API key
                - `403 Forbidden`: ingest token belongs to another tenant
                - `404 Not Found`: ingest token or tenant not found
                - `422 Unprocessable Entity`: destination URL inactive or suspended
                """)
            .RequireCors(CorsExtensions.OpenPolicyName)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(AuthExtensions.ApiKeyScheme)
                .RequireAuthenticatedUser())
            .Produces<EventAcceptedResponse>(StatusCodes.Status202Accepted)
            .Produces<EventAcceptedResponse>(StatusCodes.Status200OK)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> Handle(
        Guid tenantId,
        string token,
        [Microsoft.AspNetCore.Mvc.FromBody] JsonElement payload,
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        IDestinationUrlRepository destinationRepository,
        IWebhookSenderRepository senderRepository,
        IEventRepository eventRepository,
        ITenantRepository tenantRepository,
        IUnitOfWorkFactory uowFactory,
        IEventPublisher publisher,
        IEventIdempotencyStore idempotencyStore,
        IDestinationCacheService destinationCache,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var claimTenantId) is null && claimTenantId != tenantId)
            return Results.Forbid();

        if (InputSanitizer.ValidateToken(token) is { } tokenErr)
            return Results.BadRequest(tokenErr);

        var idempotencyKey = httpRequest.Headers["X-Idempotency-Key"].FirstOrDefault();
        if (idempotencyKey is not null)
        {
            Event? existing = null;
            try
            {
                var cachedId = await idempotencyStore.FindEventIdAsync(tenantId, idempotencyKey, ct);
                if (cachedId.HasValue)
                    existing = await eventRepository.FindAsync(cachedId.Value, ct);
            }
            catch
            {
                // Redis unavailable — fall back to DB
                existing = await eventRepository.FindByIdempotencyKeyAsync(tenantId, idempotencyKey, ct);
            }

            if (existing is not null)
                return Results.Ok(new EventAcceptedResponse(existing.Id, existing.Status.ToString(), existing.AcceptedAt));
        }

        if (token.StartsWith(IngestToken.DestinationPrefix, StringComparison.Ordinal))
            return await HandleDestinationToken(
                token, payload, tenantId, idempotencyKey,
                destinationRepository, eventRepository, tenantRepository, uowFactory,
                publisher, idempotencyStore, destinationCache, ct);

        if (token.StartsWith(IngestToken.SenderPrefix, StringComparison.Ordinal))
            return await HandleSenderToken(
                token, payload, tenantId, idempotencyKey,
                destinationRepository, senderRepository, eventRepository, tenantRepository, uowFactory,
                publisher, idempotencyStore, destinationCache, ct);

        return Results.BadRequest("Invalid token: unrecognized prefix.");
    }

    private static async Task<IResult> HandleDestinationToken(
        string token, JsonElement payload, Guid tenantId, string? idempotencyKey,
        IDestinationUrlRepository destinationRepository,
        IEventRepository eventRepository,
        ITenantRepository tenantRepository,
        IUnitOfWorkFactory uowFactory,
        IEventPublisher publisher,
        IEventIdempotencyStore idempotencyStore,
        IDestinationCacheService destinationCache, CancellationToken ct)
    {
        var tokenHash = IngestToken.Hash(token);
        var destination = await destinationRepository.FindByIngestTokenHashAsync(tokenHash, ct);

        if (destination is null) return Results.NotFound("Ingest token not found.");
        if (destination.TenantId != tenantId) return Results.Forbid();
        if (!destination.IsActive())
            return Results.UnprocessableEntity($"Destination '{destination.Id}' is not active.");

        await destinationCache.SetAsync(destination.Id, DestinationCacheEntry.From(destination), ct);

        return await CreateAndPublishEvent(
            tenantId, destination.Id, destination.Url, destination.ServerRateLimit,
            destination.AuthType, destination.CredentialsEncrypted, payload.GetRawText(),
            idempotencyKey, eventRepository, tenantRepository, uowFactory, publisher, idempotencyStore, ct);
    }

    private static async Task<IResult> HandleSenderToken(
        string token, JsonElement payload, Guid tenantId, string? idempotencyKey,
        IDestinationUrlRepository destinationRepository,
        IWebhookSenderRepository senderRepository,
        IEventRepository eventRepository,
        ITenantRepository tenantRepository,
        IUnitOfWorkFactory uowFactory,
        IEventPublisher publisher,
        IEventIdempotencyStore idempotencyStore,
        IDestinationCacheService destinationCache, CancellationToken ct)
    {
        var tokenHash = IngestToken.Hash(token);
        var sender = await senderRepository.FindByIngestTokenHashAsync(tokenHash, ct);

        if (sender is null) return Results.NotFound("Ingest token not found.");
        if (sender.TenantId != tenantId) return Results.Forbid();

        var payloadJson = payload.GetRawText();

        var entry = await destinationCache.GetAsync(sender.DestinationId, ct);
        if (entry is not null)
        {
            if (entry.Status != DestinationUrlStatus.Active)
                return Results.UnprocessableEntity($"Destination '{sender.DestinationId}' is not active.");

            if (sender.Mapping is not null)
            {
                try { payloadJson = PayloadMapper.Apply(sender.Mapping, payloadJson); }
                catch (Exception) { payloadJson = payload.GetRawText(); }
            }

            return await CreateAndPublishEvent(
                tenantId, sender.DestinationId, entry.Url, entry.ServerRateLimit,
                entry.AuthType, entry.CredentialsEncrypted, payloadJson,
                idempotencyKey, eventRepository, tenantRepository, uowFactory, publisher, idempotencyStore, ct);
        }

        var destination = await destinationRepository.FindAsync(sender.DestinationId, ct);
        if (destination is null || !destination.IsActive())
            return Results.UnprocessableEntity($"Destination '{sender.DestinationId}' is not active.");

        await destinationCache.SetAsync(destination.Id, DestinationCacheEntry.From(destination), ct);

        if (sender.Mapping is not null)
        {
            try { payloadJson = PayloadMapper.Apply(sender.Mapping, payloadJson); }
            catch (Exception) { payloadJson = payload.GetRawText(); }
        }

        return await CreateAndPublishEvent(
            tenantId, destination.Id, destination.Url, destination.ServerRateLimit,
            destination.AuthType, destination.CredentialsEncrypted, payloadJson,
            idempotencyKey, eventRepository, tenantRepository, uowFactory, publisher, idempotencyStore, ct);
    }

    private static async Task<IResult> CreateAndPublishEvent(
        Guid tenantId, Guid destinationId, string destinationUrl,
        int serverRateLimit, DestinationAuthType? authType, string? credentialsEncrypted,
        string payloadJson, string? idempotencyKey,
        IEventRepository eventRepository,
        ITenantRepository tenantRepository,
        IUnitOfWorkFactory uowFactory,
        IEventPublisher publisher,
        IEventIdempotencyStore idempotencyStore, CancellationToken ct)
    {
        var tenant = await tenantRepository.FindAsync(tenantId, ct);
        if (tenant is null)
            return Results.NotFound($"Tenant '{tenantId}' not found.");

        Event evt;
        try
        {
            evt = new Event(tenantId, destinationId, payloadJson, idempotencyKey);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        await using var uow = uowFactory.Create();
        await eventRepository.AddAsync(evt, ct);
        await uow.CommitAsync(ct);

        if (idempotencyKey is not null)
        {
            try { await idempotencyStore.StoreAsync(tenantId, idempotencyKey, evt.Id, ct); }
            catch { /* Redis unavailable — idempotency degrades to DB-only on next request */ }
        }

        await publisher.PublishAsync(new EventMessage(
            EventId: evt.Id,
            TenantId: evt.TenantId,
            DestinationUrlId: evt.DestinationUrlId,
            DestinationUrl: destinationUrl,
            Payload: evt.Payload,
            RetryCount: evt.CurrentRetryCount,
            MaxTrys: tenant.MaxTrys,
            WebhookSecret: tenant.WebhookSecret,
            ServerRateLimit: serverRateLimit,
            AuthType: authType,
            CredentialsEncrypted: credentialsEncrypted
        ), ct);

        return Results.Accepted(
            $"/api/v1/events/{evt.Id}",
            new EventAcceptedResponse(evt.Id, evt.Status.ToString(), evt.AcceptedAt));
    }
}
