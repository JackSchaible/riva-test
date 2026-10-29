using System.Net.Http.Json;
using Dapper;
using Npgsql;
using RoomBooking.Models.Responses;

namespace RoomBooking.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for API tests. Each test starts with no bookings; the seeded rooms are kept.
/// </summary>
[Collection(ApiCollection.Name)]
public abstract class ApiTestBase(BookingApiFactory factory) : IAsyncLifetime
{
    /// <summary>A fixed UTC day so tests do not depend on the clock.</summary>
    protected static readonly DateOnly Day = new(2030, 1, 15);

    // Creating the client starts the app, which runs the migrations before InitializeAsync resets data.
    protected HttpClient Client { get; } = factory.CreateClient();

    protected BookingApiFactory Factory => factory;

    public async Task InitializeAsync()
    {
        await using NpgsqlConnection connection = OpenConnection();
        await connection.ExecuteAsync("TRUNCATE bookings RESTART IDENTITY");
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>A UTC time on <see cref="Day"/> (or a nearby day via <paramref name="dayOffset"/>).</summary>
    protected static DateTimeOffset At(int hour, int minute = 0, int dayOffset = 0) =>
        new(Day.AddDays(dayOffset).ToDateTime(new TimeOnly(hour, minute)), TimeSpan.Zero);

    protected async Task<int> RoomIdAsync(string name)
    {
        await using NpgsqlConnection connection = OpenConnection();
        return await connection.QuerySingleAsync<int>("SELECT id FROM rooms WHERE name = @name", new { name });
    }

    protected Task<HttpResponseMessage> PostBookingAsync(
        int roomId, DateTimeOffset start, DateTimeOffset end, string bookedBy = "Test User") =>
        PostBookingAsync(new { roomId, bookedBy, startUtc = start, endUtc = end });

    protected Task<HttpResponseMessage> PostBookingAsync(object body) =>
        Client.PostAsJsonAsync("/api/bookings", body);

    /// <summary>Posts a booking that is expected to succeed and returns it.</summary>
    protected async Task<BookingResponse> CreateBookingAsync(
        int roomId, DateTimeOffset start, DateTimeOffset end, string bookedBy = "Test User")
    {
        HttpResponseMessage response = await PostBookingAsync(roomId, start, end, bookedBy);
        response.EnsureSuccessStatusCode();
        return (await ReadAsync<BookingResponse>(response)).Data!;
    }

    protected static async Task<ApiResponse<T>> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ApiResponse<T>>())!;

    /// <summary>Reads booking rows straight from the database, bypassing the API.</summary>
    protected async Task<List<BookingRow>> GetBookingRowsAsync()
    {
        await using NpgsqlConnection connection = OpenConnection();
        IEnumerable<BookingRow> rows = await connection.QueryAsync<BookingRow>("""
            SELECT id AS Id, room_id AS RoomId, booked_by AS BookedBy, start_utc AS StartUtc,
                   end_utc AS EndUtc, cancelled_at AS CancelledAt
            FROM bookings ORDER BY id
            """);
        return rows.ToList();
    }

    protected async Task<List<BookingRow>> GetActiveBookingRowsAsync() =>
        (await GetBookingRowsAsync()).Where(b => b.CancelledAt == null).ToList();

    private NpgsqlConnection OpenConnection() => new(factory.ConnectionString);
}

public class BookingRow
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public required string BookedBy { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public DateTime? CancelledAt { get; set; }
}
