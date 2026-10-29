using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using RoomBooking.Database;
using RoomBooking.IntegrationTests.Infrastructure;
using RoomBooking.Services.DbConnectionFactory;

namespace RoomBooking.IntegrationTests;

public class DatabaseSetupTests(BookingApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Migrations_CanRunAgain_WithoutDuplicatingRooms()
    {
        // The app already migrated at startup; a second run (e.g. a restart) must be a no-op.
        DatabaseMigrator.Migrate(Factory.ConnectionString, NullLogger.Instance);

        await using NpgsqlConnection connection = new(Factory.ConnectionString);
        IEnumerable<string> rooms = await connection.QueryAsync<string>("SELECT name FROM rooms ORDER BY name");
        Assert.Equal(["Room A", "Room B", "Room C"], rooms);
    }

    [Fact]
    public async Task DevelopmentSeeder_AddsSampleBookingsForTodayUtc_WhenThereAreNone()
    {
        DateTime today = DateTime.UtcNow.Date;

        await DevelopmentSeeder.SeedAsync(ConnectionFactory, NullLogger.Instance);

        List<BookingRow> rows = await GetBookingRowsAsync();
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Equal(today, row.StartUtc.Date));
        Assert.All(rows, row => Assert.Null(row.CancelledAt));
    }

    [Fact]
    public async Task DevelopmentSeeder_DoesNothing_WhenBookingsExist()
    {
        await CreateBookingAsync(await RoomIdAsync("Room A"), At(10), At(11));

        await DevelopmentSeeder.SeedAsync(ConnectionFactory, NullLogger.Instance);

        Assert.Single(await GetBookingRowsAsync());
    }

    private IDbConnectionFactory ConnectionFactory => Factory.Services.GetRequiredService<IDbConnectionFactory>();
}
