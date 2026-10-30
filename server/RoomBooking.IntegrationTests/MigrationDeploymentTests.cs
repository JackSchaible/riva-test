using System.Net;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using RoomBooking.Database;
using RoomBooking.IntegrationTests.Infrastructure;

namespace RoomBooking.IntegrationTests;

/// <summary>
/// The pieces the deployment relies on: serialized migration runs, skipping startup migrations, and the
/// API's least-privilege database role. Each test uses its own fresh database on the shared container.
/// </summary>
public class MigrationDeploymentTests(BookingApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Migrate_OverlappingRunsOnANewDatabase_BothSucceed()
    {
        string connectionString = NewDatabaseConnectionString();

        // Without the advisory lock, both runs would race to create the database and apply script 001.
        using Barrier barrier = new(2);
        Task Run() => Task.Run(() =>
        {
            barrier.SignalAndWait();
            DatabaseMigrator.Migrate(connectionString, NullLogger.Instance);
        });
        await Task.WhenAll(Run(), Run());

        await using NpgsqlConnection connection = new(connectionString);
        Assert.Equal(["Room A", "Room B", "Room C"], await connection.QueryAsync<string>("SELECT name FROM rooms ORDER BY name"));
        Assert.Equal(2, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM schemaversions"));
    }

    [Fact]
    public async Task MigrationModeSkip_ServesRequestsWithoutMigrating()
    {
        string connectionString = await CreateEmptyDatabaseAsync();

        await using WebApplicationFactory<Program> app = Factory.WithWebHostBuilder(builder => builder
            .UseSetting("ConnectionStrings:DefaultConnection", connectionString)
            .UseSetting("Database:Migrations", "Skip"));
        using HttpClient client = app.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        await using NpgsqlConnection connection = new(connectionString);
        Assert.Null(await connection.ExecuteScalarAsync<string>("SELECT to_regclass('public.rooms')::text"));
    }

    [Fact]
    public async Task AppRole_CanUseTheApiTablesButNothingElse()
    {
        string adminConnectionString = NewDatabaseConnectionString();
        DatabaseMigrator.Migrate(adminConnectionString, NullLogger.Instance);
        string role = NewRoleName();
        AppRoleProvisioner.Provision(adminConnectionString, role, "app'pass", NullLogger.Instance);

        await using NpgsqlConnection app = new(AsRole(adminConnectionString, role, "app'pass"));
        int roomId = await app.QuerySingleAsync<int>("SELECT id FROM rooms WHERE name = 'Room A'");
        int bookingId = await app.QuerySingleAsync<int>("""
            INSERT INTO bookings (room_id, booked_by, start_utc, end_utc)
            VALUES (@roomId, 'Alice', '2030-01-15T10:00Z', '2030-01-15T11:00Z') RETURNING id
            """, new { roomId });
        await app.ExecuteAsync("UPDATE bookings SET cancelled_at = now() WHERE id = @bookingId", new { bookingId });

        await AssertDeniedAsync(app, "DELETE FROM bookings");
        await AssertDeniedAsync(app, "INSERT INTO rooms (name) VALUES ('Room D')");
        await AssertDeniedAsync(app, "SELECT * FROM schemaversions");
        await AssertDeniedAsync(app, "CREATE TABLE intruder (id int)");
        await AssertDeniedAsync(app, "DROP TABLE bookings");
    }

    [Fact]
    public async Task AppRole_ProvisioningAgainRotatesThePassword()
    {
        string adminConnectionString = NewDatabaseConnectionString();
        DatabaseMigrator.Migrate(adminConnectionString, NullLogger.Instance);
        string role = NewRoleName();

        AppRoleProvisioner.Provision(adminConnectionString, role, "first", NullLogger.Instance);
        AppRoleProvisioner.Provision(adminConnectionString, role, "second", NullLogger.Instance);

        await using NpgsqlConnection app = new(AsRole(adminConnectionString, role, "second"));
        Assert.Equal(3, await app.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM rooms"));
        await using NpgsqlConnection stale = new(AsRole(adminConnectionString, role, "first"));
        PostgresException error = await Assert.ThrowsAsync<PostgresException>(() => stale.OpenAsync());
        Assert.Equal(PostgresErrorCodes.InvalidPassword, error.SqlState);
    }

    [Theory]
    [InlineData("App")]
    [InlineData("app; DROP TABLE bookings")]
    [InlineData("")]
    public void AppRole_RejectsUnsafeRoleNames(string role)
    {
        Assert.Throws<ArgumentException>(() =>
            AppRoleProvisioner.Provision(Factory.ConnectionString, role, "pass", NullLogger.Instance));
    }

    private static async Task AssertDeniedAsync(NpgsqlConnection connection, string sql)
    {
        PostgresException error = await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(sql));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    private string NewDatabaseConnectionString() =>
        new NpgsqlConnectionStringBuilder(Factory.ConnectionString) { Database = $"migrate_{Guid.NewGuid():N}" }
            .ConnectionString;

    private async Task<string> CreateEmptyDatabaseAsync()
    {
        string connectionString = NewDatabaseConnectionString();
        await using NpgsqlConnection admin = new(Factory.ConnectionString);
        await admin.ExecuteAsync($"CREATE DATABASE \"{new NpgsqlConnectionStringBuilder(connectionString).Database}\"");
        return connectionString;
    }

    private static string NewRoleName() => $"app_{Guid.NewGuid():N}"[..20];

    private static string AsRole(string connectionString, string role, string password) =>
        new NpgsqlConnectionStringBuilder(connectionString) { Username = role, Password = password, Pooling = false }
            .ConnectionString;
}
