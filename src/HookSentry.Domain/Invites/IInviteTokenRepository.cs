using HookSentry.Domain.Repositories;

namespace HookSentry.Domain.Invites;

public interface IInviteTokenRepository : IRepository<InviteToken>
{
    Task<InviteToken?> FindByTokenAsync(string token, CancellationToken ct = default);
}
