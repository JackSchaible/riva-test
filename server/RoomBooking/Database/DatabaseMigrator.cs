using System.Reflection;
using DbUp;
using DbUp.Engine;
using DbUp.Engine.Output;

namespace RoomBooking.Database;

public static class DatabaseMigrator
{
    /// <summary>
    /// Creates the database if needed and applies any embedded SQL migrations that have not yet run.
    /// </summary>
    public static void Migrate(string connectionString, ILogger logger)
    {
        EnsureDatabase.For.PostgresqlDatabase(connectionString, new MicrosoftUpgradeLog(logger));

        UpgradeEngine upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .WithTransactionPerScript()
            .LogTo(new MicrosoftUpgradeLog(logger))
            .Build();

        DatabaseUpgradeResult result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            throw new InvalidOperationException(
                $"Database migration failed on script '{result.ErrorScript?.Name}'", result.Error);
        }
    }
}
