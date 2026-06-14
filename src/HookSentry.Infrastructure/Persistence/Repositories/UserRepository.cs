using HookSentry.Domain.Users;
using NHibernate;
using NHibernate.Linq;

namespace HookSentry.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(ISession session)
    : NHibernateRepository<User>(session), IUserRepository
{
    public async Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken ct = default)
        => await Session.Query<User>()
            .Where(u => u.Email == normalizedEmail)
            .SingleOrDefaultAsync(ct);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default)
        => Session.Query<User>()
            .AnyAsync(u => u.Email == normalizedEmail, ct);

    public Task<bool> EmailExistsExcludingAsync(string normalizedEmail, Guid excludeId, CancellationToken ct = default)
        => Session.Query<User>()
            .AnyAsync(u => u.Email == normalizedEmail && u.Id != excludeId, ct);
}
