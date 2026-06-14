namespace HookSentry.Domain.Events;

public class Event
{
    public virtual Guid Id { get; protected set; }
    public virtual Guid TenantId { get; protected set; }
    public virtual Guid DestinationUrlId { get; protected set; }
    public virtual string Payload { get; protected set; } = default!;
    public virtual EventStatus Status { get; protected set; }
    public virtual string? IdempotencyKey { get; protected set; }
    public virtual int CurrentRetryCount { get; protected set; }
    public virtual DateTimeOffset? NextAttemptAt { get; protected set; }
    public virtual DateTimeOffset AcceptedAt { get; protected set; }
    public virtual DateTimeOffset? DeliveredAt { get; protected set; }

    protected Event() { }

    public Event(Guid tenantId, Guid destinationUrlId, string payload, string? idempotencyKey = null)
    {
        SetTenantId(tenantId);
        SetDestinationUrlId(destinationUrlId);
        SetPayload(payload);
        SetIdempotencyKey(idempotencyKey);

        Id = Guid.NewGuid();
        Status = EventStatus.Pending;
        CurrentRetryCount = 0;
        AcceptedAt = DateTimeOffset.UtcNow;
    }

    private void SetTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId não pode ser vazio.", nameof(tenantId));
        TenantId = tenantId;
    }

    private void SetDestinationUrlId(Guid destinationUrlId)
    {
        if (destinationUrlId == Guid.Empty)
            throw new ArgumentException("DestinationUrlId não pode ser vazio.", nameof(destinationUrlId));
        DestinationUrlId = destinationUrlId;
    }

    public virtual void SetPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Payload não pode ser nulo ou vazio.", nameof(payload));

        try { System.Text.Json.JsonDocument.Parse(payload); }
        catch (System.Text.Json.JsonException)
        {
            throw new ArgumentException("Payload deve ser um JSON válido.", nameof(payload));
        }

        Payload = payload;
    }

    public virtual void ResetForReplay()
    {
        if (Status != EventStatus.CriticalFailure)
            throw new InvalidOperationException(
                "Somente eventos com status 'CriticalFailure' podem ser reenviados.");

        CurrentRetryCount = 0;
        NextAttemptAt = DateTimeOffset.UtcNow;
        Status = EventStatus.Pending;
    }

    public virtual void Cancel()
    {
        if (Status != EventStatus.Pending && Status != EventStatus.WaitingRetry)
            throw new InvalidOperationException(
                "Somente eventos com status 'Pending' ou 'WaitingRetry' podem ser cancelados.");

        Status = EventStatus.Cancelled;
    }

    public virtual void MarkSucceeded()
    {
        if (!IsActiveStatus())
            throw new InvalidOperationException(
                "Apenas eventos em estado ativo (Pending, Processing ou WaitingRetry) podem ser marcados como Succeeded.");
        Status = EventStatus.Succeeded;
        DeliveredAt = DateTimeOffset.UtcNow;
    }

    public virtual void MarkWaitingRetry(int retryCount, DateTimeOffset nextAttemptAt)
    {
        if (!IsActiveStatus())
            throw new InvalidOperationException(
                "Apenas eventos em estado ativo (Pending, Processing ou WaitingRetry) podem ser marcados como WaitingRetry.");
        Status = EventStatus.WaitingRetry;
        CurrentRetryCount = retryCount;
        NextAttemptAt = nextAttemptAt;
    }

    public virtual void MarkCriticalFailure()
    {
        if (!IsActiveStatus())
            throw new InvalidOperationException(
                "Apenas eventos em estado ativo (Pending, Processing ou WaitingRetry) podem ser marcados como CriticalFailure.");
        Status = EventStatus.CriticalFailure;
    }

    public virtual void MarkAuthenticationFailed()
    {
        if (!IsActiveStatus())
            throw new InvalidOperationException(
                "Apenas eventos em estado ativo (Pending, Processing ou WaitingRetry) podem ser marcados como AuthenticationFailed.");
        Status = EventStatus.AuthenticationFailed;
    }

    private bool IsActiveStatus() =>
        Status is EventStatus.Pending or EventStatus.Processing or EventStatus.WaitingRetry;

    private void SetIdempotencyKey(string? key)
    {
        if (key is not null && key.Length > 255)
            throw new ArgumentException(
                "IdempotencyKey não pode exceder 255 caracteres.", nameof(key));
        IdempotencyKey = key;
    }
}
