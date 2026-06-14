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
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        TenantId = tenantId;
    }

    private void SetDestinationUrlId(Guid destinationUrlId)
    {
        if (destinationUrlId == Guid.Empty)
            throw new ArgumentException("DestinationUrlId cannot be empty.", nameof(destinationUrlId));
        DestinationUrlId = destinationUrlId;
    }

    public virtual void SetPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Payload cannot be null or empty.", nameof(payload));

        try { System.Text.Json.JsonDocument.Parse(payload); }
        catch (System.Text.Json.JsonException)
        {
            throw new ArgumentException("Payload must be valid JSON.", nameof(payload));
        }

        Payload = payload;
    }

    public virtual void ResetForReplay()
    {
        if (Status != EventStatus.CriticalFailure)
            throw new InvalidOperationException(
                "Only events with 'CriticalFailure' status can be replayed.");

        CurrentRetryCount = 0;
        NextAttemptAt = DateTimeOffset.UtcNow;
        Status = EventStatus.Pending;
    }

    public virtual void Cancel()
    {
        if (Status != EventStatus.Pending && Status != EventStatus.WaitingRetry)
            throw new InvalidOperationException(
                "Only events with 'Pending' or 'WaitingRetry' status can be cancelled.");

        Status = EventStatus.Cancelled;
    }

    public virtual void MarkSucceeded()
    {
        if (!IsActiveStatus())
            throw new InvalidOperationException(
                "Only events in an active state (Pending, Processing or WaitingRetry) can be marked as Succeeded.");
        Status = EventStatus.Succeeded;
        DeliveredAt = DateTimeOffset.UtcNow;
    }

    public virtual void MarkWaitingRetry(int retryCount, DateTimeOffset nextAttemptAt)
    {
        if (!IsActiveStatus())
            throw new InvalidOperationException(
                "Only events in an active state (Pending, Processing or WaitingRetry) can be marked as WaitingRetry.");
        Status = EventStatus.WaitingRetry;
        CurrentRetryCount = retryCount;
        NextAttemptAt = nextAttemptAt;
    }

    public virtual void MarkCriticalFailure()
    {
        if (!IsActiveStatus())
            throw new InvalidOperationException(
                "Only events in an active state (Pending, Processing or WaitingRetry) can be marked as CriticalFailure.");
        Status = EventStatus.CriticalFailure;
    }

    public virtual void MarkAuthenticationFailed()
    {
        if (!IsActiveStatus())
            throw new InvalidOperationException(
                "Only events in an active state (Pending, Processing or WaitingRetry) can be marked as AuthenticationFailed.");
        Status = EventStatus.AuthenticationFailed;
    }

    private bool IsActiveStatus() =>
        Status is EventStatus.Pending or EventStatus.Processing or EventStatus.WaitingRetry;

    private void SetIdempotencyKey(string? key)
    {
        if (key is not null && key.Length > 255)
            throw new ArgumentException(
                "IdempotencyKey cannot exceed 255 characters.", nameof(key));
        IdempotencyKey = key;
    }
}
