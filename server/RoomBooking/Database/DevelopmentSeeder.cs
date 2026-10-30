using Dapper;
using RoomBooking.Services.DbConnectionFactory;

namespace RoomBooking.Database;

public static class DevelopmentSeeder
{
    private const string CountQuery = "SELECT COUNT(*) FROM bookings";

    private const string InsertQuery = """
                                       INSERT INTO bookings (room_id, booked_by, start_utc, end_utc)
                                       SELECT r.id, @BookedBy, @StartUtc, @EndUtc
                                       FROM rooms r
                                       WHERE r.name = @RoomName
                                       """;

    /// <summary>
    /// Adds a few sample bookings for the current UTC day, but only when there are no bookings yet.
    /// </summary>
    public static async Task SeedAsync(IDbConnectionFactory connectionFactory, ILogger logger)
    {
        await using var connection = connectionFactory.CreateConnection();

        if (await connection.ExecuteScalarAsync<long>(CountQuery) > 0)
        {
            return;
        }

        DateTime today = DateTime.UtcNow.Date;

        object[] samples =
        [
            Sample("Room A", "Alice", today.AddHours(9), today.AddHours(10)),
            Sample("Room A", "Bob", today.AddHours(10), today.AddHours(11)),
            Sample("Room B", "Carol", today.AddHours(10), today.AddHours(11).AddMinutes(30)),
            Sample("Room C", "Dave", today.AddHours(14), today.AddHours(15)),
        ];

        await connection.ExecuteAsync(InsertQuery, samples);
        logger.LogInformation("Seeded {Count} sample bookings for {Date:yyyy-MM-dd}", samples.Length, today);
    }

    private static object Sample(string roomName, string bookedBy, DateTime startUtc, DateTime endUtc) =>
        new { RoomName = roomName, BookedBy = bookedBy, StartUtc = startUtc, EndUtc = endUtc };
}
