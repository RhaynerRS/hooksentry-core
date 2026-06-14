using HookSentry.Domain.Senders;
using NHibernate;
using NHibernate.Linq;

namespace HookSentry.Infrastructure.Persistence.Repositories;

public sealed class WebhookSenderRepository(ISession session)
    : NHibernateRepository<WebhookSender>(session), IWebhookSenderRepository
{
    public async Task<WebhookSender?> FindByIngestTokenHashAsync(string tokenHash, CancellationToken ct = default)
        => await Session.Query<WebhookSender>()
            .Where(s => s.IngestTokenHash == tokenHash)
            .SingleOrDefaultAsync(ct);
}
