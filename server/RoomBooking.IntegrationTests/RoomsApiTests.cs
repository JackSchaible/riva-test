using System.Net;
using RoomBooking.IntegrationTests.Infrastructure;
using RoomBooking.Models.Responses;

namespace RoomBooking.IntegrationTests;

public class RoomsApiTests(BookingApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task GetRooms_ReturnsTheThreeSeededRooms()
    {
        HttpResponseMessage response = await Client.GetAsync("/api/rooms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ApiResponse<List<RoomResponse>> body = await ReadAsync<List<RoomResponse>>(response);
        Assert.True(body.Success);
        Assert.Equal(["Room A", "Room B", "Room C"], body.Data!.Select(r => r.Name));
    }
}
