using System.Reflection;
using DbUp;
using DbUp.Engine;
using DbUp.Engine.Output;
using Npgsql;

namespace RoomBooking.Database;

public static class DatabaseMigrator
{
    // Arbitrary but fixed: every migration runner must use the same advisory lock key.
    private const long MigrationLockKey = 0x526F6F6D426B67; // "RoomBkg"

    /// <summary>
    /// Creates the database if needed and applies any embedded SQL migrations that have not yet run.
    /// </summary>
    /// <remarks>
    /// The whole run holds a PostgreSQL advisory lock, so overlapping runners (for example two containers
    /// starting at once) wait for each other instead of racing. The lock is taken in the maintenance
    /// database, because the target database may not exist yet and advisory locks are per database.
    /// </remarks>
    public static void Migrate(string connectionString, ILogger logger)
    {
        using NpgsqlConnection lockConnection = new(MaintenanceConnectionString(connectionString));
        lockConnection.Open();
        using (NpgsqlCommand acquire = new("SELECT pg_advisory_lock(@key)", lockConnection))
        {
            acquire.Parameters.AddWithValue("key", MigrationLockKey);
            acquire.ExecuteNonQuery();
        }

        // Closing the connection releases the session-level lock, including when a migration fails.
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

    /// <summary>The same server and credentials, connected to the "postgres" database DbUp also uses to create the target.</summary>
    private static string MaintenanceConnectionString(string connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres", Pooling = false }.ConnectionString;
}
