using System.Net;
using RoomBooking.IntegrationTests.Infrastructure;
using RoomBooking.Models.Responses;

namespace RoomBooking.IntegrationTests;

/// <summary>
/// Verifies the booking rules through the API and checks what was actually saved in the database.
/// </summary>
public class BookingRulesTests(BookingApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task OverlappingBooking_Returns409WithUsefulMessageAndLeavesOneBooking()
    {
        int roomA = await RoomIdAsync("Room A");
        await CreateBookingAsync(roomA, At(10), At(11));

        HttpResponseMessage response = await PostBookingAsync(roomA, At(10, 30), At(11, 30));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        ApiResponse<object> body = await ReadAsync<object>(response);
        Assert.False(body.Success);
        Assert.Equal("Room A is already booked 10:00-11:00 UTC", body.Message);

        BookingRow row = Assert.Single(await GetBookingRowsAsync());
        Assert.Equal(At(10).UtcDateTime, row.StartUtc);
    }

    [Theory]
    [InlineData(10, 0, 11, 0)] // identical
    [InlineData(9, 30, 10, 30)] // overlaps the start
    [InlineData(10, 15, 10, 45)] // inside
    [InlineData(9, 0, 12, 0)] // surrounds
    public async Task AnyOverlapInTheSameRoom_Returns409(int startHour, int startMinute, int endHour, int endMinute)
    {
        int roomA = await RoomIdAsync("Room A");
        await CreateBookingAsync(roomA, At(10), At(11));

        HttpResponseMessage response = await PostBookingAsync(
            roomA, At(startHour, startMinute), At(endHour, endMinute));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single(await GetBookingRowsAsync());
    }

    [Fact]
    public async Task AdjacentBookings_AreBothSaved()
    {
        int roomA = await RoomIdAsync("Room A");
        await CreateBookingAsync(roomA, At(10), At(11));

        HttpResponseMessage after = await PostBookingAsync(roomA, At(11), At(12));
        HttpResponseMessage before = await PostBookingAsync(roomA, At(9), At(10));

        Assert.Equal(HttpStatusCode.Created, after.StatusCode);
        Assert.Equal(HttpStatusCode.Created, before.StatusCode);
        Assert.Equal(3, (await GetActiveBookingRowsAsync()).Count);
    }

    [Fact]
    public async Task SameTimeInDifferentRooms_AreBothSaved()
    {
        int roomA = await RoomIdAsync("Room A");
        int roomB = await RoomIdAsync("Room B");

        HttpResponseMessage first = await PostBookingAsync(roomA, At(10), At(11));
        HttpResponseMessage second = await PostBookingAsync(roomB, At(10), At(11));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        List<BookingRow> rows = await GetActiveBookingRowsAsync();
        Assert.Equal([roomA, roomB], rows.Select(r => r.RoomId).Order());
    }

    public static TheoryData<string, string, string> InvalidTimes => new()
    {
        { "2030-01-15T10:00:00Z", "2030-01-15T10:00:00Z", "End time must be after start time" },
        { "2030-01-15T11:00:00Z", "2030-01-15T10:00:00Z", "End time must be after start time" },
        {
            "2030-01-15T23:00:00Z", "2030-01-16T01:00:00Z",
            "Booking must be within a single UTC day (ending no later than 00:00 UTC the next day)"
        },
        { "2030-01-15T10:00:00+02:00", "2030-01-15T11:00:00+02:00", "Start time must be in UTC (offset +00:00 or Z)" },
        { "2030-01-15T10:00:00Z", "2030-01-15T11:00:00-05:00", "End time must be in UTC (offset +00:00 or Z)" },
    };

    [Theory]
    [MemberData(nameof(InvalidTimes))]
    public async Task InvalidTimes_Return400AndWriteNothing(string startUtc, string endUtc, string error)
    {
        HttpResponseMessage response = await PostBookingAsync(new
        {
            roomId = await RoomIdAsync("Room A"),
            bookedBy = "Alice",
            startUtc,
            endUtc
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ApiResponse<object> body = await ReadAsync<object>(response);
        Assert.Contains(error, body.Errors!);
        Assert.Empty(await GetBookingRowsAsync());
    }

    [Fact]
    public async Task CancelledSlot_CanBeBookedAgain()
    {
        int roomA = await RoomIdAsync("Room A");
        BookingResponse original = await CreateBookingAsync(roomA, At(10), At(11), "Alice");
        (await Client.PostAsync($"/api/bookings/{original.Id}/cancel", null)).EnsureSuccessStatusCode();

        HttpResponseMessage response = await PostBookingAsync(roomA, At(10), At(11), "Bob");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        List<BookingRow> rows = await GetBookingRowsAsync();
        Assert.Equal(2, rows.Count);
        Assert.NotNull(rows.Single(r => r.BookedBy == "Alice").CancelledAt);
        Assert.Null(rows.Single(r => r.BookedBy == "Bob").CancelledAt);
    }
}
