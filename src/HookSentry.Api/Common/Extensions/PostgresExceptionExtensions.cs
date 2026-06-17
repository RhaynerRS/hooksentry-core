using Npgsql;

namespace HookSentry.Api.Common.Extensions;

internal static class PostgresExceptionExtensions
{
    private const string UniqueViolation = "23505";

    public static bool IsUniqueViolation(this Exception ex) =>
        ex is PostgresException pg && pg.SqlState == UniqueViolation
        || ex.InnerException is PostgresException inner && inner.SqlState == UniqueViolation;
}
