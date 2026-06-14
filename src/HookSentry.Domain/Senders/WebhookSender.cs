using System.Text.Json;
using HookSentry.Domain.Common;

namespace HookSentry.Domain.Senders;

public class WebhookSender
{
    public virtual Guid Id { get; protected set; }
    public virtual Guid DestinationId { get; protected set; }
    public virtual Guid TenantId { get; protected set; }
    public virtual string? Label { get; protected set; }
    public virtual string? IngestTokenHash { get; protected set; }
    public virtual string? Mapping { get; protected set; }
    public virtual DateTimeOffset CreatedAt { get; protected set; }
    public virtual DateTimeOffset UpdatedAt { get; protected set; }

    protected WebhookSender() { }

    public WebhookSender(Guid destinationId, Guid tenantId, string? label = null)
    {
        SetDestinationId(destinationId);
        SetTenantId(tenantId);
        if (label is not null) SetLabel(label);

        Id = Guid.NewGuid();
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    public virtual void SetDestinationId(Guid destinationId)
    {
        if (destinationId == Guid.Empty)
            throw new ArgumentException("DestinationId cannot be empty.", nameof(destinationId));
        DestinationId = destinationId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public virtual void SetTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        TenantId = tenantId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public virtual void SetLabel(string? label)
    {
        if (label is not null && label.Length > 255)
            throw new ArgumentException("Label cannot exceed 255 characters.", nameof(label));
        Label = label;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public virtual string RotateIngestToken()
    {
        var (rawToken, hash) = IngestToken.Generate(IngestToken.SenderPrefix);
        IngestTokenHash = hash;
        UpdatedAt = DateTimeOffset.UtcNow;
        return rawToken;
    }

    public virtual void SetMapping(string? mappingJson)
    {
        if (mappingJson is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(mappingJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    throw new ArgumentException("Mapping must be a valid JSON object.", nameof(mappingJson));
            }
            catch (JsonException)
            {
                throw new ArgumentException("Mapping must be valid JSON.", nameof(mappingJson));
            }
        }

        Mapping = mappingJson;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
