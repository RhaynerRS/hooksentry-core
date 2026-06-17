using DbUp;
using DbUp.Engine.Output;
using Microsoft.Extensions.Logging;

namespace HookSentry.Infrastructure.Persistence;

public static class DatabaseMigrator
{
    public static void Migrate(string connectionString, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Migrations");

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                typeof(DatabaseMigrator).Assembly,
                s => s.Contains(".Persistence.Migrations."))
            .WithTransactionPerScript()
            .LogTo(new DbUpLogAdapter(logger))
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
            throw new InvalidOperationException("Database migration failed.", result.Error);
    }

    private sealed class DbUpLogAdapter(ILogger logger) : IUpgradeLog
    {
        public void WriteInformation(string format, params object[] args) =>
            logger.LogInformation(format, args);

        public void WriteWarning(string format, params object[] args) =>
            logger.LogWarning(format, args);

        public void WriteError(string format, params object[] args) =>
            logger.LogError(format, args);
    }
}
