using System.Security.Claims;
using System.Text.Json;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.Common.Validation;
using HookSentry.Api.DataTransfer.Events.Responses;
using HookSentry.Domain.Common;
using HookSentry.Domain.Destinations;
using HookSentry.Domain.Events;
using HookSentry.Domain.Senders;
using HookSentry.Domain.Tenants;
using HookSentry.Infrastructure.Destinations;
using HookSentry.Infrastructure.RabbitMq;
using NHibernate.Linq;
using StackExchange.Redis;

namespace HookSentry.Api.Features.Ingest;

public class IngestEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/ingest/{tenantId:guid}/{token}", Handle)
            .WithName("IngestEvent")
            .WithTags("Ingest")
            .WithSummary("Ingere um evento via ingest token")
            .WithDescription("""
                Recebe um payload JSON arbitrário e o persiste para entrega assíncrona na URL de destino
                associada ao ingest token informado.

                **Dois tipos de token são aceitos:**
                - `dst_<token>` — token da URL de destino; payload entregue sem transformação
                - `sndr_<token>` — token de sender; payload transformado pelo mapeamento configurado (se houver)

                Configure a URL `https://{host}/api/v1/ingest/{tenantId}/{token}` diretamente no serviço
                externo que emite os webhooks — sem necessidade de estruturar o payload.

                **Parâmetros de rota:**
                - `tenantId` *(obrigatório)*: UUID do tenant
                - `token` *(obrigatório)*: ingest token da URL de destino ou do sender

                **Headers:**
                - `X-Api-Key` *(obrigatório)*: chave de API para autenticação
                - `X-Idempotency-Key` *(opcional)*: chave de até 255 caracteres — se já existir um evento
                  com a mesma chave para este tenant, retorna `200 OK` com os dados do evento original
                  sem reprocessar

                **Body:**
                - Objeto JSON arbitrário a ser entregue na URL de destino

                **Códigos de retorno:**
                - `202 Accepted`: evento aceito para entrega assíncrona
                - `200 OK`: evento duplicado retornado via idempotency key
                - `400 Bad Request`: payload inválido ou token com formato inválido
                - `401 Unauthorized`: API key ausente ou inválida
                - `403 Forbidden`: ingest token pertence a outro tenant
                - `404 Not Found`: ingest token ou tenant não encontrado
                - `422 Unprocessable Entity`: URL de destino inativa ou suspensa
                """)
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
        NHibernate.ISession session,
        IEventPublisher publisher,
        IConnectionMultiplexer redis,
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
                var cached = await redis.GetDatabase()
                    .StringGetAsync($"idempotency:{tenantId}:{idempotencyKey}");

                if (cached.HasValue && Guid.TryParse(cached.ToString(), out var cachedId))
                    existing = await session.GetAsync<Event>(cachedId, ct);
            }
            catch
            {
                // Redis unavailable — fall back to DB
                existing = await session.Query<Event>()
                    .Where(e => e.TenantId == tenantId && e.IdempotencyKey == idempotencyKey)
                    .SingleOrDefaultAsync(ct);
            }

            if (existing is not null)
                return Results.Ok(new EventAcceptedResponse(existing.Id, existing.Status.ToString(), existing.AcceptedAt));
        }

        if (token.StartsWith(IngestToken.DestinationPrefix, StringComparison.Ordinal))
            return await HandleDestinationToken(token, payload, tenantId, idempotencyKey, session, publisher, redis, destinationCache, ct);

        if (token.StartsWith(IngestToken.SenderPrefix, StringComparison.Ordinal))
            return await HandleSenderToken(token, payload, tenantId, idempotencyKey, session, publisher, redis, destinationCache, ct);

        return Results.BadRequest("Token inválido: prefixo não reconhecido.");
    }

    private static async Task<IResult> HandleDestinationToken(
        string token, JsonElement payload, Guid tenantId, string? idempotencyKey,
        NHibernate.ISession session, IEventPublisher publisher, IConnectionMultiplexer redis,
        IDestinationCacheService destinationCache, CancellationToken ct)
    {
        var tokenHash = IngestToken.Hash(token);
        var destination = await session.Query<DestinationUrl>()
            .Where(d => d.IngestTokenHash == tokenHash)
            .SingleOrDefaultAsync(ct);

        if (destination is null) return Results.NotFound("Ingest token não encontrado.");
        if (destination.TenantId != tenantId) return Results.Forbid();
        if (!destination.IsActive())
            return Results.UnprocessableEntity($"Destination '{destination.Id}' is not active.");

        await destinationCache.SetAsync(destination.Id, DestinationCacheEntry.From(destination), ct);

        return await CreateAndPublishEvent(
            tenantId, destination.Id, destination.Url, destination.ServerRateLimit,
            destination.AuthType, destination.CredentialsEncrypted, payload.GetRawText(),
            idempotencyKey, session, publisher, redis, ct);
    }

    private static async Task<IResult> HandleSenderToken(
        string token, JsonElement payload, Guid tenantId, string? idempotencyKey,
        NHibernate.ISession session, IEventPublisher publisher, IConnectionMultiplexer redis,
        IDestinationCacheService destinationCache, CancellationToken ct)
    {
        var tokenHash = IngestToken.Hash(token);
        var sender = await session.Query<WebhookSender>()
            .Where(s => s.IngestTokenHash == tokenHash)
            .SingleOrDefaultAsync(ct);

        if (sender is null) return Results.NotFound("Ingest token não encontrado.");
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
                idempotencyKey, session, publisher, redis, ct);
        }

        var destination = await session.GetAsync<DestinationUrl>(sender.DestinationId, ct);
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
            idempotencyKey, session, publisher, redis, ct);
    }

    private static async Task<IResult> CreateAndPublishEvent(
        Guid tenantId, Guid destinationId, string destinationUrl,
        int serverRateLimit, DestinationAuthType? authType, string? credentialsEncrypted,
        string payloadJson, string? idempotencyKey,
        NHibernate.ISession session, IEventPublisher publisher, IConnectionMultiplexer redis, CancellationToken ct)
    {
        var tenant = await session.GetAsync<Tenant>(tenantId, ct);
        if (tenant is null)
            return Results.NotFound($"Tenant '{tenantId}' not found.");

        Event evento;
        try
        {
            evento = new Event(tenantId, destinationId, payloadJson, idempotencyKey);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        using var tx = session.BeginTransaction();
        await session.SaveAsync(evento, ct);
        await tx.CommitAsync(ct);

        if (idempotencyKey is not null)
        {
            try
            {
                await redis.GetDatabase().StringSetAsync(
                    $"idempotency:{tenantId}:{idempotencyKey}",
                    evento.Id.ToString(),
                    TimeSpan.FromHours(24));
            }
            catch { /* Redis unavailable — idempotency degrades to DB-only on next request */ }
        }

        await publisher.PublishAsync(new EventMessage(
            EventId: evento.Id,
            TenantId: evento.TenantId,
            DestinationUrlId: evento.DestinationUrlId,
            DestinationUrl: destinationUrl,
            Payload: evento.Payload,
            RetryCount: evento.CurrentRetryCount,
            MaxTrys: tenant.MaxTrys,
            WebhookSecret: tenant.WebhookSecret,
            ServerRateLimit: serverRateLimit,
            AuthType: authType,
            CredentialsEncrypted: credentialsEncrypted
        ), ct);

        return Results.Accepted(
            $"/api/v1/events/{evento.Id}",
            new EventAcceptedResponse(evento.Id, evento.Status.ToString(), evento.AcceptedAt));
    }
}
