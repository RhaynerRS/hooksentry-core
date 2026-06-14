using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HookSentry.Domain.Destinations;
using HookSentry.Domain.Events;
using HookSentry.Infrastructure.RabbitMq;
using HookSentry.Infrastructure.Security;
using Microsoft.Extensions.Options;
using NHibernate;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace HookSentry.Worker.Consumers;

public sealed class WebhookDeliveryConsumer(
    RabbitMqConnection mqConnection,
    IOptions<RabbitMqSettings> options,
    ICredentialEncryptionService encryption,
    IEventPublisher publisher,
    ISessionFactory sessionFactory,
    ILogger<WebhookDeliveryConsumer> logger) : BackgroundService
{
    private readonly string _exchange = options.Value.EventsExchange;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _semaphores = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("WebhookDeliveryConsumer starting...");

        var channel = await mqConnection.GetConnection().CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(
            exchange: _exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: "webhooks.delivery",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueBindAsync(
            queue: "webhooks.delivery",
            exchange: _exchange,
            routingKey: "tenant.#",
            cancellationToken: stoppingToken);

        await DeclareDelayQueuesAsync(channel, stoppingToken);

        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: options.Value.PrefetchCount, global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            EventMessage? message = null;
            try
            {
               message = JsonSerializer.Deserialize<EventMessage>(ea.Body.Span);

                if (message is null)
                {
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false,
                        cancellationToken: stoppingToken);
                    return;
                }

                logger.LogInformation(
                    "Event received: {EventId} -> {DestinationUrl} (retry #{RetryCount})",
                    message.EventId, message.DestinationUrl, message.RetryCount);

                var semaphore = GetSemaphore(message.DestinationUrlId, message.ServerRateLimit);
                await semaphore.WaitAsync(stoppingToken);
                HttpResponseMessage response;
                try
                {
                    using var httpClient = new HttpClient();
                    await ApplyAuthAsync(httpClient, message);

                    var signature = ComputeSignature(message.WebhookSecret, message.Payload);
                    httpClient.DefaultRequestHeaders.Add("X-HookSentry-Signature", signature);

                    var content = new StringContent(message.Payload, Encoding.UTF8, "application/json");
                    response = await httpClient.PostAsync(message.DestinationUrl, content, stoppingToken);
                }
                finally
                {
                    semaphore.Release();
                }

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
                {
                    logger.LogWarning(
                        "Authentication failed ({StatusCode}) for event {EventId}. Removing from queue.",
                        (int)response.StatusCode, message.EventId);

                    await MarkAuthenticationFailedAsync(message.EventId, stoppingToken);
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false,
                        cancellationToken: stoppingToken);
                    return;
                }

                if (response.IsSuccessStatusCode)
                {
                    await MarkSucceededAsync(message.EventId, stoppingToken);
                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false,
                        cancellationToken: stoppingToken);
                    return;
                }

                // Falha não-auth: aplicar backoff
                var nextRetry = message.RetryCount + 1;
                var nextAt = DateTimeOffset.UtcNow + GetBackoffDuration(nextRetry);

                if (nextRetry >= message.MaxTrys)
                {
                    logger.LogWarning(
                        "Event {EventId} exhausted {MaxTrys} retries. Marking as CriticalFailure.",
                        message.EventId, message.MaxTrys);

                    await MarkCriticalFailureAsync(message.EventId, stoppingToken);
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false,
                        cancellationToken: stoppingToken);
                    return;
                }

                await MarkWaitingRetryAsync(message.EventId, nextRetry, nextAt, stoppingToken);
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false,
                    cancellationToken: stoppingToken);
                await publisher.PublishDelayedAsync(
                    message with { RetryCount = nextRetry }, nextRetry, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process message with DeliveryTag {Tag}", ea.DeliveryTag);

                if (message is not null)
                {
                    var nextRetry = message.RetryCount + 1;
                    var nextAt = DateTimeOffset.UtcNow + GetBackoffDuration(nextRetry);

                    if (nextRetry >= message.MaxTrys)
                    {
                        await MarkCriticalFailureAsync(message.EventId, stoppingToken);
                        await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false,
                            cancellationToken: stoppingToken);
                        return;
                    }

                    await MarkWaitingRetryAsync(message.EventId, nextRetry, nextAt, stoppingToken);
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false,
                        cancellationToken: stoppingToken);
                    await publisher.PublishDelayedAsync(
                        message with { RetryCount = nextRetry }, nextRetry, stoppingToken);
                }
                else
                {
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false,
                        cancellationToken: stoppingToken);
                }
            }
        };

        await channel.BasicConsumeAsync(
            queue: "webhooks.delivery",
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation("WebhookDeliveryConsumer listening on 'webhooks.delivery'...");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private SemaphoreSlim GetSemaphore(Guid destinationId, int limit)
        => _semaphores.GetOrAdd(destinationId, _ => new SemaphoreSlim(limit, limit));

    private static string ComputeSignature(string secret, string payload)
    {
        var keyBytes  = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(payload);
        var hash      = HMACSHA256.HashData(keyBytes, dataBytes);
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task DeclareDelayQueuesAsync(IChannel channel, CancellationToken ct)
    {
        var delayQueues = new[]
        {
            ("hooksentry.delay.2m",  120_000),
            ("hooksentry.delay.5m",  300_000),
            ("hooksentry.delay.15m", 900_000),
            ("hooksentry.delay.1h",  3_600_000),
            ("hooksentry.delay.6h",  21_600_000),
        };

        foreach (var (queue, ttl) in delayQueues)
        {
            await channel.QueueDeclareAsync(
                queue: queue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-message-ttl"]             = ttl,
                    ["x-dead-letter-exchange"]    = "hooksentry.events",
                    ["x-dead-letter-routing-key"] = "tenant.retry"
                },
                cancellationToken: ct);
        }
    }

    private static TimeSpan GetBackoffDuration(int retryCount) => retryCount switch
    {
        1 => TimeSpan.FromMinutes(2),
        2 => TimeSpan.FromMinutes(5),
        3 => TimeSpan.FromMinutes(15),
        4 => TimeSpan.FromHours(1),
        _ => TimeSpan.FromHours(6)
    };

    private async Task ApplyAuthAsync(HttpClient client, EventMessage message)
    {
        if (message.AuthType is null || message.CredentialsEncrypted is null)
            return;

        var credentialsJson = encryption.Decrypt(message.CredentialsEncrypted);
        using var credentials = JsonDocument.Parse(credentialsJson);
        var root = credentials.RootElement;

        switch (message.AuthType)
        {
            case DestinationAuthType.ApiKey:
                var headerName = root.GetProperty("headerName").GetString()!;
                var key = root.GetProperty("value").GetString()!;
                client.DefaultRequestHeaders.Add(headerName, key);
                break;

            case DestinationAuthType.BearerToken:
                var token = root.GetProperty("token").GetString()!;
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
                break;

            case DestinationAuthType.JwtBearer:
                var accessToken = await FetchJwtTokenAsync(root);
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
                break;

            case DestinationAuthType.BasicAuth:
                var username = root.GetProperty("username").GetString()!;
                var password = root.GetProperty("password").GetString()!;
                var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", encoded);
                break;
        }
    }

    private static async Task<string> FetchJwtTokenAsync(JsonElement credentials)
    {
        var clientId = credentials.GetProperty("clientId").GetString()!;
        var clientSecret = credentials.GetProperty("clientSecret").GetString()!;
        var tokenUrl = credentials.GetProperty("tokenEndpoint").GetString()!;
        var scope = credentials.TryGetProperty("scope", out var s) ? s.GetString() : null;

        using var client = new HttpClient();
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
        };
        if (scope is not null) form["scope"] = scope;

        var response = await client.PostAsync(tokenUrl, new FormUrlEncodedContent(form));
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("access_token").GetString()!;
    }

    private async Task MarkSucceededAsync(Guid eventId, CancellationToken ct)
    {
        using var session = sessionFactory.OpenSession();
        using var tx = session.BeginTransaction();
        var evento = await session.GetAsync<Event>(eventId, ct);
        if (evento is not null)
        {
            evento.MarkSucceeded();
            await tx.CommitAsync(ct);
        }
    }

    private async Task MarkWaitingRetryAsync(Guid eventId, int retryCount, DateTimeOffset nextAttemptAt, CancellationToken ct)
    {
        using var session = sessionFactory.OpenSession();
        using var tx = session.BeginTransaction();
        var evento = await session.GetAsync<Event>(eventId, ct);
        if (evento is not null)
        {
            evento.MarkWaitingRetry(retryCount, nextAttemptAt);
            await tx.CommitAsync(ct);
        }
    }

    private async Task MarkCriticalFailureAsync(Guid eventId, CancellationToken ct)
    {
        using var session = sessionFactory.OpenSession();
        using var tx = session.BeginTransaction();
        var evento = await session.GetAsync<Event>(eventId, ct);
        if (evento is not null)
        {
            evento.MarkCriticalFailure();
            await tx.CommitAsync(ct);
        }
    }

    private async Task MarkAuthenticationFailedAsync(Guid eventId, CancellationToken ct)
    {
        using var session = sessionFactory.OpenSession();
        using var tx = session.BeginTransaction();
        var evento = await session.GetAsync<Event>(eventId, ct);
        if (evento is not null)
        {
            evento.MarkAuthenticationFailed();
            await tx.CommitAsync(ct);
        }
    }
}
