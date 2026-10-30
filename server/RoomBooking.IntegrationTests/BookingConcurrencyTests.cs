using System.Net;
using System.Net.Http.Json;
using RoomBooking.IntegrationTests.Infrastructure;

namespace RoomBooking.IntegrationTests;

public class BookingConcurrencyTests(BookingApiFactory factory) : ApiTestBase(factory)
{
    private const int Rounds = 25;

    public static TheoryData<int> RoundNumbers => new(Enumerable.Range(1, Rounds));

    /// <summary>
    /// Releases two overlapping requests for the same room at the same moment. Exactly one may win.
    /// Each round is a separate test case, so the data is reset between rounds.
    /// </summary>
    [Theory]
    [MemberData(nameof(RoundNumbers))]
    public async Task SimultaneousOverlappingRequests_ExactlyOneSucceeds(int round)
    {
        int roomA = await RoomIdAsync("Room A");

        HttpResponseMessage[] responses = await SendTogetherAsync(
            new { roomId = roomA, bookedBy = $"First {round}", startUtc = At(10), endUtc = At(11) },
            new { roomId = roomA, bookedBy = $"Second {round}", startUtc = At(10, 30), endUtc = At(11, 30) });

        HttpStatusCode[] statuses = responses.Select(r => r.StatusCode).Order().ToArray();
        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], statuses);
        Assert.Single(await GetActiveBookingRowsAsync());
        Assert.Single(await GetBookingRowsAsync());
    }

    /// <summary>
    /// Adjacent bookings arriving together must not block each other: the constraint only rejects real overlaps.
    /// </summary>
    [Theory]
    [MemberData(nameof(RoundNumbers))]
    public async Task SimultaneousAdjacentRequests_BothSucceed(int round)
    {
        int roomA = await RoomIdAsync("Room A");

        HttpResponseMessage[] responses = await SendTogetherAsync(
            new { roomId = roomA, bookedBy = $"First {round}", startUtc = At(10), endUtc = At(11) },
            new { roomId = roomA, bookedBy = $"Second {round}", startUtc = At(11), endUtc = At(12) });

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.Equal(2, (await GetActiveBookingRowsAsync()).Count);
    }

    /// <summary>
    /// A booking and a cancellation of the booking it overlaps race each other. Either may win:
    /// cancel first means the new booking succeeds; insert first means it conflicts. Whichever happens,
    /// the cancel succeeds, nothing fails with a 500, and the database agrees with the responses.
    /// </summary>
    [Theory]
    [MemberData(nameof(RoundNumbers))]
    public async Task SimultaneousCancelAndOverlappingBooking_LeaveConsistentState(int round)
    {
        int roomA = await RoomIdAsync("Room A");
        int existingId = (await CreateBookingAsync(roomA, At(10), At(11), $"Existing {round}")).Id;

        HttpResponseMessage[] responses = await RunTogetherAsync(
            client => client.PostAsync($"/api/bookings/{existingId}/cancel", null),
            client => client.PostAsJsonAsync("/api/bookings",
                new { roomId = roomA, bookedBy = $"New {round}", startUtc = At(10, 30), endUtc = At(11, 30) }));
        HttpResponseMessage cancel = responses[0];
        HttpResponseMessage create = responses[1];

        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Contains(create.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });

        List<BookingRow> rows = await GetBookingRowsAsync();
        Assert.NotNull(rows.Single(r => r.Id == existingId).CancelledAt);

        if (create.StatusCode == HttpStatusCode.Created)
        {
            Assert.Equal(2, rows.Count);
            Assert.Equal($"New {round}", Assert.Single(await GetActiveBookingRowsAsync()).BookedBy);
        }
        else
        {
            Assert.Single(rows);
            Assert.Empty(await GetActiveBookingRowsAsync());
        }
    }

    /// <summary>
    /// Posts both bookings from separate clients, released together.
    /// </summary>
    private Task<HttpResponseMessage[]> SendTogetherAsync(object first, object second) =>
        RunTogetherAsync(
            client => client.PostAsJsonAsync("/api/bookings", first),
            client => client.PostAsJsonAsync("/api/bookings", second));

    /// <summary>
    /// Runs two requests from separate clients, holding each at a barrier so they are released together.
    /// Responses are returned in the order the requests were given.
    /// </summary>
    private async Task<HttpResponseMessage[]> RunTogetherAsync(
        Func<HttpClient, Task<HttpResponseMessage>> first, Func<HttpClient, Task<HttpResponseMessage>> second)
    {
        using HttpClient otherClient = Factory.CreateClient();
        using Barrier barrier = new(2);

        Task<HttpResponseMessage> Run(HttpClient client, Func<HttpClient, Task<HttpResponseMessage>> request) =>
            Task.Run(async () =>
            {
                barrier.SignalAndWait();
                return await request(client);
            });

        return await Task.WhenAll(Run(Client, first), Run(otherClient, second));
    }
}
