namespace RoomBooking.Services;

using Dapper;
using DbConnectionFactory;
using Exceptions;
using Models.Data;
using Npgsql;

public class BookingService(IDbConnectionFactory connectionFactory) : IBookingService
{
    public const int MaxDeadlockAttempts = 3;

    private const string SelectColumns = """
                                         b.id AS Id, b.room_id AS RoomId, r.name AS RoomName, b.booked_by AS BookedBy,
                                         b.start_utc AS StartUtc, b.end_utc AS EndUtc,
                                         b.created_at AS CreatedAt, b.cancelled_at AS CancelledAt
                                         """;

    private const string GetForDayQuery = $"""
                                           SELECT {SelectColumns}
                                           FROM bookings b JOIN rooms r ON r.id = b.room_id
                                           WHERE b.cancelled_at IS NULL
                                             AND b.start_utc >= @DayStart AND b.start_utc < @DayEnd
                                           ORDER BY r.name, b.start_utc
                                           """;

    private const string GetByIdQuery = $"""
                                         SELECT {SelectColumns}
                                         FROM bookings b JOIN rooms r ON r.id = b.room_id
                                         WHERE b.id = @Id
                                         """;

    // No overlap pre-check: the bookings_no_overlap exclusion constraint rejects the insert atomically,
    // which is what makes simultaneous requests safe.
    private const string CreateQuery = $"""
                                        WITH b AS (
                                            INSERT INTO bookings (room_id, booked_by, start_utc, end_utc)
                                            VALUES (@RoomId, @BookedBy, @StartUtc, @EndUtc)
                                            RETURNING *
                                        )
                                        SELECT {SelectColumns}
                                        FROM b JOIN rooms r ON r.id = b.room_id
                                        """;

    // COALESCE keeps the original cancellation time when a booking is cancelled again.
    private const string CancelQuery = $"""
                                        WITH b AS (
                                            UPDATE bookings SET cancelled_at = COALESCE(cancelled_at, now())
                                            WHERE id = @Id
                                            RETURNING *
                                        )
                                        SELECT {SelectColumns}
                                        FROM b JOIN rooms r ON r.id = b.room_id
                                        """;

    private const string FindConflictQuery = $"""
                                              SELECT {SelectColumns}
                                              FROM bookings b JOIN rooms r ON r.id = b.room_id
                                              WHERE b.room_id = @RoomId AND b.cancelled_at IS NULL
                                                AND b.start_utc < @EndUtc AND b.end_utc > @StartUtc
                                              ORDER BY b.start_utc
                                              LIMIT 1
                                              """;

    private const string GetRoomNameQuery = "SELECT name FROM rooms WHERE id = @RoomId";

    public async Task<List<Booking>> GetForDayAsync(DateOnly day)
    {
        DateTime dayStart = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        await using var connection = connectionFactory.CreateConnection();
        IEnumerable<Booking> bookings = await connection.QueryAsync<Booking>(
            GetForDayQuery, new { DayStart = dayStart, DayEnd = dayStart.AddDays(1) });
        return bookings.ToList();
    }

    public async Task<Booking?> GetByIdAsync(int id)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Booking>(GetByIdQuery, new { Id = id });
    }

    public async Task<Booking> CreateAsync(int roomId, string bookedBy, DateTime startUtc, DateTime endUtc)
    {
        var parameters = new { RoomId = roomId, BookedBy = bookedBy, StartUtc = startUtc, EndUtc = endUtc };

        await using var connection = connectionFactory.CreateConnection();

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await InsertAsync(connection, parameters);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ExclusionViolation)
            {
                throw new BookingConflictException(await DescribeConflictAsync(connection, parameters));
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            {
                throw new RoomNotFoundException(roomId);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.DeadlockDetected
                                               && attempt < MaxDeadlockAttempts)
            {
                // Two simultaneous overlapping inserts can each wait on the other's uncommitted row in the
                // exclusion constraint's index; PostgreSQL then aborts one of them. Retrying lets the survivor
                // commit first, so the retry fails with a normal exclusion violation (or succeeds if it rolled back).
            }
        }
    }

    /// <summary>
    /// One insert attempt. Virtual so tests can simulate PostgreSQL errors (such as a deadlock)
    /// that real concurrent requests only produce intermittently.
    /// </summary>
    protected virtual Task<Booking> InsertAsync(NpgsqlConnection connection, object parameters) =>
        connection.QuerySingleAsync<Booking>(CreateQuery, parameters);

    public async Task<Booking?> CancelAsync(int id)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Booking>(CancelQuery, new { Id = id });
    }

    private static async Task<string> DescribeConflictAsync(NpgsqlConnection connection, object parameters)
    {
        Booking? conflict = await connection.QueryFirstOrDefaultAsync<Booking>(FindConflictQuery, parameters);

        if (conflict != null)
        {
            return $"{conflict.RoomName} is already booked {conflict.StartUtc:HH:mm}-{conflict.EndUtc:HH:mm} UTC";
        }

        // The conflicting booking was cancelled between our insert and this lookup.
        string? roomName = await connection.QuerySingleOrDefaultAsync<string>(GetRoomNameQuery, parameters);
        return $"{roomName ?? "The room"} is already booked for part of that time";
    }
}
