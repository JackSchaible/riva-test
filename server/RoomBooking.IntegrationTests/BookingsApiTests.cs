using System.Net;
using System.Text;
using RoomBooking.IntegrationTests.Infrastructure;
using RoomBooking.Json;
using RoomBooking.Models.Responses;

namespace RoomBooking.IntegrationTests;

/// <summary>
/// Covers each bookings endpoint: list by day, get by ID, create, and cancel.
/// </summary>
public class BookingsApiTests(BookingApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task GetBookings_ReturnsActiveBookingsStartingOnTheRequestedUtcDayOnly()
    {
        int roomA = await RoomIdAsync("Room A");
        int roomB = await RoomIdAsync("Room B");

        await CreateBookingAsync(roomA, At(23, dayOffset: -1), At(0), "Previous day");
        BookingResponse firstOfDay = await CreateBookingAsync(roomA, At(0), At(1), "Start of day");
        BookingResponse otherRoom = await CreateBookingAsync(roomB, At(9), At(10), "Other room");
        BookingResponse lastOfDay = await CreateBookingAsync(roomA, At(23), At(0, dayOffset: 1), "End of day");
        await CreateBookingAsync(roomA, At(0, dayOffset: 1), At(1, dayOffset: 1), "Next day");
        BookingResponse cancelled = await CreateBookingAsync(await RoomIdAsync("Room C"), At(12), At(13));
        await Client.PostAsync($"/api/bookings/{cancelled.Id}/cancel", null);

        HttpResponseMessage response = await Client.GetAsync($"/api/bookings?date={Day:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<BookingResponse> bookings = (await ReadAsync<List<BookingResponse>>(response)).Data!;
        Assert.Equal(
            new[] { firstOfDay.Id, lastOfDay.Id, otherRoom.Id },
            bookings.Select(b => b.Id));
        Assert.DoesNotContain(bookings, b => b.Id == cancelled.Id);
        Assert.All(bookings, b => Assert.Equal(TimeSpan.Zero, b.StartUtc.Offset));
    }

    [Theory]
    [InlineData("")]
    [InlineData("?date=not-a-date")]
    public async Task GetBookings_WithMissingOrInvalidDate_Returns400(string query)
    {
        HttpResponseMessage response = await Client.GetAsync($"/api/bookings{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ApiResponse<object> body = await ReadAsync<object>(response);
        Assert.False(body.Success);
        Assert.NotEmpty(body.Errors!);
    }

    [Fact]
    public async Task GetBooking_ReturnsTheBooking()
    {
        BookingResponse created = await CreateBookingAsync(await RoomIdAsync("Room B"), At(13), At(14), "Carol");

        HttpResponseMessage response = await Client.GetAsync($"/api/bookings/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        BookingResponse booking = (await ReadAsync<BookingResponse>(response)).Data!;
        Assert.Equal(created.Id, booking.Id);
        Assert.Equal("Room B", booking.RoomName);
        Assert.Equal("Carol", booking.BookedBy);
        Assert.Equal(At(13), booking.StartUtc);
        Assert.Equal(At(14), booking.EndUtc);
        Assert.Null(booking.CancelledAt);
    }

    [Fact]
    public async Task GetBooking_WithUnknownId_Returns404()
    {
        HttpResponseMessage response = await Client.GetAsync("/api/bookings/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False((await ReadAsync<object>(response)).Success);
    }

    [Fact]
    public async Task CreateBooking_WithValidRequest_Returns201AndPersistsTheBooking()
    {
        int roomA = await RoomIdAsync("Room A");

        HttpResponseMessage response = await PostBookingAsync(roomA, At(10), At(11), "  Alice  ");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        BookingResponse booking = (await ReadAsync<BookingResponse>(response)).Data!;
        Assert.Equal($"/api/bookings/{booking.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal("Room A", booking.RoomName);
        Assert.Equal("Alice", booking.BookedBy);

        BookingRow row = Assert.Single(await GetBookingRowsAsync());
        Assert.Equal(booking.Id, row.Id);
        Assert.Equal(roomA, row.RoomId);
        Assert.Equal("Alice", row.BookedBy);
        Assert.Equal(At(10).UtcDateTime, row.StartUtc);
        Assert.Equal(At(11).UtcDateTime, row.EndUtc);
        Assert.Null(row.CancelledAt);
    }

    [Fact]
    public async Task CreateBooking_EndingAtMidnightUtc_IsAllowed()
    {
        HttpResponseMessage response = await PostBookingAsync(
            await RoomIdAsync("Room A"), At(23), At(0, dayOffset: 1));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Single(await GetBookingRowsAsync());
    }

    [Fact]
    public async Task CreateBooking_ForTheWholeUtcDay_IsAllowed()
    {
        HttpResponseMessage response = await PostBookingAsync(
            await RoomIdAsync("Room A"), At(0), At(0, dayOffset: 1));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Single(await GetBookingRowsAsync());
    }

    [Fact]
    public async Task CreateBooking_WithNameOfMaximumLength_IsAllowed()
    {
        string name = new('x', 100);

        HttpResponseMessage response = await PostBookingAsync(await RoomIdAsync("Room A"), At(10), At(11), name);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(name, Assert.Single(await GetBookingRowsAsync()).BookedBy);
    }

    [Theory]
    [InlineData("2030-01-15T10:00:00", "2030-01-15T11:00:00Z")]
    [InlineData("2030-01-15T10:00:00Z", "2030-01-15T11:00:00")]
    [InlineData("10:00", "2030-01-15T11:00:00Z")]
    public async Task CreateBooking_WithTimeMissingAnExplicitOffset_Returns400AndWritesNothing(
        string startUtc, string endUtc)
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
        Assert.Equal([ExplicitOffsetDateTimeOffsetConverter.ErrorMessage], body.Errors!);
        Assert.Empty(await GetBookingRowsAsync());
    }

    [Theory]
    [InlineData("{\"roomId\": 1, \"bookedBy\": ")]
    [InlineData("null")]
    [InlineData("")]
    public async Task CreateBooking_WithMalformedOrMissingBody_Returns400Envelope(string json)
    {
        HttpResponseMessage response = await Client.PostAsync("/api/bookings",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ApiResponse<object> body = await ReadAsync<object>(response);
        Assert.False(body.Success);
        Assert.Equal("Validation failed", body.Message);
        Assert.NotEmpty(body.Errors!);
        Assert.Empty(await GetBookingRowsAsync());
    }

    public static TheoryData<object, string> InvalidRequests => new()
    {
        { new { roomId = 1, bookedBy = "", startUtc = At(10), endUtc = At(11) }, "Name is required" },
        { new { roomId = 1, bookedBy = "   ", startUtc = At(10), endUtc = At(11) }, "Name is required" },
        {
            new { roomId = 1, bookedBy = new string('x', 101), startUtc = At(10), endUtc = At(11) },
            "Name must not exceed 100 characters"
        },
        { new { bookedBy = "Alice", startUtc = At(10), endUtc = At(11) }, "Room is required" },
        { new { roomId = 999, bookedBy = "Alice", startUtc = At(10), endUtc = At(11) }, "Room 999 does not exist" },
        { new { roomId = 1, bookedBy = "Alice", endUtc = At(11) }, "Start time is required" },
        { new { roomId = 1, bookedBy = "Alice", startUtc = At(10) }, "End time is required" },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task CreateBooking_WithInvalidRequest_Returns400AndWritesNothing(object request, string error)
    {
        HttpResponseMessage response = await PostBookingAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ApiResponse<object> body = await ReadAsync<object>(response);
        Assert.Equal("Validation failed", body.Message);
        Assert.Contains(error, body.Errors!);
        Assert.Empty(await GetBookingRowsAsync());
    }

    [Fact]
    public async Task CancelBooking_SetsCancelledAt()
    {
        BookingResponse created = await CreateBookingAsync(await RoomIdAsync("Room A"), At(10), At(11));

        HttpResponseMessage response = await Client.PostAsync($"/api/bookings/{created.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        BookingResponse cancelled = (await ReadAsync<BookingResponse>(response)).Data!;
        Assert.NotNull(cancelled.CancelledAt);

        BookingRow row = Assert.Single(await GetBookingRowsAsync());
        Assert.NotNull(row.CancelledAt);
    }

    [Fact]
    public async Task CancelBooking_Twice_IsIdempotent()
    {
        BookingResponse created = await CreateBookingAsync(await RoomIdAsync("Room A"), At(10), At(11));
        await Client.PostAsync($"/api/bookings/{created.Id}/cancel", null);
        DateTime? firstCancelledAt = Assert.Single(await GetBookingRowsAsync()).CancelledAt;

        HttpResponseMessage response = await Client.PostAsync($"/api/bookings/{created.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(firstCancelledAt, Assert.Single(await GetBookingRowsAsync()).CancelledAt);
    }

    [Fact]
    public async Task CancelBooking_WithUnknownId_Returns404()
    {
        HttpResponseMessage response = await Client.PostAsync("/api/bookings/999/cancel", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False((await ReadAsync<object>(response)).Success);
    }
}
