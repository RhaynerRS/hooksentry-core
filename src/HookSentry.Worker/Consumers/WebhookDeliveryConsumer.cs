using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HookSentry.Domain;
using HookSentry.Domain.Destinations;
using HookSentry.Domain.Events;
using HookSentry.Infrastructure.RabbitMq;
using HookSentry.Domain.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace HookSentry.Worker.Consumers;

public sealed class WebhookDeliveryConsumer(
    RabbitMqConnection mqConnection,
    IOptions<RabbitMqSettings> options,
    ICredentialEncryptionService encryption,
    IEventPublisher publisher,
    IServiceScopeFactory scopeFactory,
    ILogger<WebhookDeliveryConsumer> logger) : BackgroundService
{
    private static readonly ActivitySource _source = new("HookSentry.Worker");

    private static readonly Action<ILogger, Exception?> _logStarting =
        LoggerMessage.Define(LogLevel.Information, default, "WebhookDeliveryConsumer starting...");

    private static readonly Action<ILogger, Exception?> _logListening =
        LoggerMessage.Define(LogLevel.Information, default, "WebhookDeliveryConsumer listening on 'webhooks.delivery'...");

    private static readonly Action<ILogger, Guid, string, int, Exception?> _logEventReceived =
        LoggerMessage.Define<Guid, string, int>(LogLevel.Information, default,
            "Event received: {EventId} -> {DestinationUrl} (retry #{RetryCount})");

    private static readonly Action<ILogger, ulong, Exception?> _logMessageProcessingFailed =
        LoggerMessage.Define<ulong>(LogLevel.Error, default,
            "Failed to process message with DeliveryTag {Tag}");

    private readonly string _exchange = options.Value.EventsExchange;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _semaphores = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logStarting(logger, null);

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
        consumer.ReceivedAsync += async (_, ea) => await DispatchAsync(channel, ea, stoppingToken);

        await channel.BasicConsumeAsync(
            queue: "webhooks.delivery",
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logListening(logger, null);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task DispatchAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        EventMessage? message = null;
        Activity? activity = null;
        try
        {
            message = JsonSerializer.Deserialize<EventMessage>(ea.Body.Span);
            if (message is null)
            {
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
                return;
            }

            _logEventReceived(logger, message.EventId, message.DestinationUrl, message.RetryCount, null);

            activity = _source.StartActivity("webhook.delivery.attempt");
            activity?.SetTag("event.id", message.EventId);
            activity?.SetTag("tenant.id", message.TenantId);
            activity?.SetTag("destination.id", message.DestinationUrlId);
            activity?.SetTag("http.attempt_number", message.RetryCount + 1);

            await DeliverAsync(message, channel, ea.DeliveryTag, activity, ct);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            _logMessageProcessingFailed(logger, ea.DeliveryTag, ex);

            if (message is not null)
                await RetryOrGiveUpAsync(message, channel, ea.DeliveryTag, ct);
            else
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
        }
        finally
        {
            activity?.Dispose();
        }
    }

    private async Task DeliverAsync(
        EventMessage message, IChannel channel, ulong deliveryTag, Activity? activity, CancellationToken ct)
    {
        var response = await SendAsync(message, ct);
        activity?.SetTag("http.response.status_code", (int)response.StatusCode);

        if (response.IsSuccessStatusCode)
        {
            activity?.SetStatus(ActivityStatusCode.Ok);
            await MarkSucceededAsync(message.EventId, ct);
            await channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken: ct);
            return;
        }

        activity?.SetStatus(ActivityStatusCode.Error);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            await HandleAuthFailureAsync(message, response, channel, deliveryTag, ct);
            return;
        }

        await HandleHttpFailureAsync(message, response, channel, deliveryTag, ct);
    }

    private async Task<HttpResponseMessage> SendAsync(EventMessage message, CancellationToken ct)
    {
        var semaphore = GetSemaphore(message.DestinationUrlId, message.ServerRateLimit);
        await semaphore.WaitAsync(ct);
        try
        {
            using var httpClient = new HttpClient();
            await ApplyAuthAsync(httpClient, message);
            httpClient.DefaultRequestHeaders.Add(
                "X-HookSentry-Signature", ComputeSignature(message.WebhookSecret, message.Payload));
            var content = new StringContent(message.Payload, Encoding.UTF8, "application/json");
            return await httpClient.PostAsync(message.DestinationUrl, content, ct);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task HandleAuthFailureAsync(
        EventMessage message, HttpResponseMessage response, IChannel channel, ulong deliveryTag, CancellationToken ct)
    {
        await LogDeliveryFailureAsync(message, response, "AuthenticationFailed", ct);
        await MarkAuthenticationFailedAsync(message.EventId, ct);
        await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken: ct);
    }

    private async Task HandleHttpFailureAsync(
        EventMessage message, HttpResponseMessage response, IChannel channel, ulong deliveryTag, CancellationToken ct)
    {
        var nextRetry = message.RetryCount + 1;
        var errorType = nextRetry >= message.MaxTrys ? "RetryExhausted" : "HttpError";
        await LogDeliveryFailureAsync(message, response, errorType, ct);
        await RetryOrGiveUpAsync(message, channel, deliveryTag, ct);
    }

    private async Task LogDeliveryFailureAsync(
        EventMessage message, HttpResponseMessage response, string errorType, CancellationToken ct)
    {
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        logger.LogWarning(
            "Webhook delivery failed. EventId={EventId} TenantId={TenantId} " +
            "DestinationId={DestinationId} Attempt={Attempt} " +
            "HttpStatus={HttpStatus} ErrorType={ErrorType} Body={ResponseBody}",
            message.EventId, message.TenantId, message.DestinationUrlId,
            message.RetryCount + 1, (int)response.StatusCode,
            errorType, responseBody[..Math.Min(responseBody.Length, 2048)]);
    }

    private async Task RetryOrGiveUpAsync(
        EventMessage message, IChannel channel, ulong deliveryTag, CancellationToken ct)
    {
        var nextRetry = message.RetryCount + 1;

        if (nextRetry >= message.MaxTrys)
        {
            await MarkCriticalFailureAsync(message.EventId, ct);
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken: ct);
            return;
        }

        var nextAt = DateTimeOffset.UtcNow + GetBackoffDuration(nextRetry);
        await MarkWaitingRetryAsync(message.EventId, nextRetry, nextAt, ct);
        await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken: ct);
        await publisher.PublishDelayedAsync(message with { RetryCount = nextRetry }, nextRetry, ct);
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

    private Task MarkSucceededAsync(Guid eventId, CancellationToken ct) =>
        UpdateEventAsync(eventId, e => e.MarkSucceeded(), ct);

    private Task MarkWaitingRetryAsync(Guid eventId, int retryCount, DateTimeOffset nextAttemptAt, CancellationToken ct) =>
        UpdateEventAsync(eventId, e => e.MarkWaitingRetry(retryCount, nextAttemptAt), ct);

    private Task MarkCriticalFailureAsync(Guid eventId, CancellationToken ct) =>
        UpdateEventAsync(eventId, e => e.MarkCriticalFailure(), ct);

    private Task MarkAuthenticationFailedAsync(Guid eventId, CancellationToken ct) =>
        UpdateEventAsync(eventId, e => e.MarkAuthenticationFailed(), ct);

    private async Task UpdateEventAsync(Guid eventId, Action<Event> update, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var uowFactory = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>();

        await using var uow = uowFactory.Create();
        var evento = await repo.FindAsync(eventId, ct);
        if (evento is not null)
        {
            update(evento);
            await uow.CommitAsync(ct);
        }
    }
}