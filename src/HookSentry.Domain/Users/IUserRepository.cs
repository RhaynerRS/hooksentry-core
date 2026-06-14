using HookSentry.Domain.Repositories;

namespace HookSentry.Domain.Users;

public interface IUserRepository : IRepository<User>
{
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default);
    Task<bool> EmailExistsExcludingAsync(string normalizedEmail, Guid excludeId, CancellationToken ct = default);
}
