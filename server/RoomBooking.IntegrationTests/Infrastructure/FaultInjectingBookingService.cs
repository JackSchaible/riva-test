using Npgsql;
using RoomBooking.Models.Data;
using RoomBooking.Services;
using RoomBooking.Services.DbConnectionFactory;

namespace RoomBooking.IntegrationTests.Infrastructure;

/// <summary>
/// A <see cref="BookingService"/> whose insert attempts first fail with the given PostgreSQL error codes,
/// in order, before running the real insert. Lets tests drive error paths that real concurrency only
/// produces intermittently.
/// </summary>
public class FaultInjectingBookingService(IDbConnectionFactory connectionFactory, params string[] sqlStates)
    : BookingService(connectionFactory)
{
    private readonly Queue<string> _sqlStates = new(sqlStates);

    /// <summary>When true, every attempt fails with the last configured error code.</summary>
    public bool RepeatLastError { get; init; }

    public int Attempts { get; private set; }

    protected override Task<Booking> InsertAsync(NpgsqlConnection connection, object parameters)
    {
        Attempts++;

        if (_sqlStates.Count > 0)
        {
            string sqlState = RepeatLastError && _sqlStates.Count == 1 ? _sqlStates.Peek() : _sqlStates.Dequeue();
            throw new PostgresException("Injected by test", "ERROR", "ERROR", sqlState);
        }

        return base.InsertAsync(connection, parameters);
    }
}
