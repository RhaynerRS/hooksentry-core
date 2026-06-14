using HookSentry.Domain.Repositories;

namespace HookSentry.Domain.Senders;

public interface IWebhookSenderRepository : IRepository<WebhookSender>
{
    Task<WebhookSender?> FindByIngestTokenHashAsync(string tokenHash, CancellationToken ct = default);
}
