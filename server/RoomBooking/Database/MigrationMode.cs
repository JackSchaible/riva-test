namespace RoomBooking.Database;

/// <summary>
/// When the API runs database migrations, set by the <c>Database:Migrations</c> setting.
/// </summary>
public enum MigrationMode
{
    /// <summary>Migrate, then serve requests. The default, used for local development and tests.</summary>
    OnStartup,

    /// <summary>Serve requests without migrating; a separate <see cref="Only"/> run has already migrated.</summary>
    Skip,

    /// <summary>Migrate (and provision the app role, if configured), then exit without serving requests.</summary>
    Only,
}
