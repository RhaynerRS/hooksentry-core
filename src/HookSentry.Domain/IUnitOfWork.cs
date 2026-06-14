namespace HookSentry.Domain;

public interface IUnitOfWork : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
}
