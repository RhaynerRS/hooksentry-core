using HookSentry.Domain.Invites;
using NHibernate;
using NHibernate.Linq;

namespace HookSentry.Infrastructure.Persistence.Repositories;

public sealed class InviteTokenRepository(ISession session)
    : NHibernateRepository<InviteToken>(session), IInviteTokenRepository
{
    public async Task<InviteToken?> FindByTokenAsync(string token, CancellationToken ct = default)
        => await Session.Query<InviteToken>()
            .Where(t => t.Token == token)
            .FirstOrDefaultAsync(ct);
}
