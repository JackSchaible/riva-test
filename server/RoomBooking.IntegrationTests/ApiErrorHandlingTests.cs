using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RoomBooking.IntegrationTests.Infrastructure;
using RoomBooking.Models.Responses;
using RoomBooking.Services;
using RoomBooking.Services.DbConnectionFactory;

namespace RoomBooking.IntegrationTests;

/// <summary>
/// Unexpected failures return the ApiResponse envelope with a generic message and no exception details.
/// </summary>
public class ApiErrorHandlingTests(BookingApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task UnexpectedException_Returns500EnvelopeWithoutDetails()
    {
        await using WebApplicationFactory<Program> failing = WithBookingService(_ => new ThrowingBookingService());
        using HttpClient client = failing.CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/api/bookings?date={Day:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        string content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(ThrowingBookingService.Detail, content);
        ApiResponse<object> body = await ReadAsync<object>(response);
        Assert.False(body.Success);
        Assert.Equal("An unexpected error occurred", body.Message);
    }

    [Fact]
    public async Task UnexpectedException_KeepsCorsHeaders_WhenCorsIsConfigured()
    {
        // Without the header, a browser would hide the 500 envelope from the deployed frontend.
        await using WebApplicationFactory<Program> failing =
            HealthAndCorsTests.WithAllowedOrigin(WithBookingService(_ => new ThrowingBookingService()));
        using HttpClient client = failing.CreateClient();

        HttpResponseMessage response = await client.SendAsync(HealthAndCorsTests.WithOrigin(
            HttpMethod.Get, $"/api/bookings?date={Day:yyyy-MM-dd}", HealthAndCorsTests.AllowedOrigin));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(HealthAndCorsTests.AllowedOrigin,
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task PersistentDeadlock_Returns500AndWritesNothing()
    {
        await using WebApplicationFactory<Program> deadlocking = WithBookingService(services =>
            new FaultInjectingBookingService(
                services.GetRequiredService<IDbConnectionFactory>(), PostgresErrorCodes.DeadlockDetected)
            {
                RepeatLastError = true
            });
        using HttpClient client = deadlocking.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/bookings",
            new { roomId = await RoomIdAsync("Room A"), bookedBy = "Alice", startUtc = At(10), endUtc = At(11) });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("An unexpected error occurred", (await ReadAsync<object>(response)).Message);
        Assert.Empty(await GetBookingRowsAsync());
    }

    private WebApplicationFactory<Program> WithBookingService(Func<IServiceProvider, IBookingService> create) =>
        Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped(create)));

    private sealed class ThrowingBookingService : IBookingService
    {
        public const string Detail = "sensitive internal detail";

        public Task<List<Models.Data.Booking>> GetForDayAsync(DateOnly day) =>
            throw new InvalidOperationException(Detail);

        public Task<Models.Data.Booking?> GetByIdAsync(int id) => throw new InvalidOperationException(Detail);

        public Task<Models.Data.Booking> CreateAsync(int roomId, string bookedBy, DateTime startUtc, DateTime endUtc) =>
            throw new InvalidOperationException(Detail);

        public Task<Models.Data.Booking?> CancelAsync(int id) => throw new InvalidOperationException(Detail);
    }
}
