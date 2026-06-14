namespace HookSentry.Domain;

public interface IUnitOfWorkFactory
{
    IUnitOfWork Create();
}
