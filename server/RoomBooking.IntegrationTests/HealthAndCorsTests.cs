using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RoomBooking.IntegrationTests.Infrastructure;

namespace RoomBooking.IntegrationTests;

/// <summary>
/// The health check, and CORS, which is off by default and only allows the configured origin when set.
/// </summary>
public class HealthAndCorsTests(BookingApiFactory factory) : ApiTestBase(factory)
{
    public const string AllowedOrigin = "https://rooms.example";

    [Fact]
    public async Task Health_Returns200()
    {
        HttpResponseMessage response = await Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Cors_IsOffByDefault()
    {
        HttpResponseMessage preflight = await Client.SendAsync(Preflight("/api/bookings", AllowedOrigin));
        HttpResponseMessage get = await Client.SendAsync(WithOrigin(HttpMethod.Get, "/api/rooms", AllowedOrigin));

        Assert.False(preflight.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(get.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task Cors_AnswersPreflightForTheConfiguredOrigin()
    {
        await using WebApplicationFactory<Program> app = WithAllowedOrigin(Factory);
        using HttpClient client = app.CreateClient();

        HttpResponseMessage response = await client.SendAsync(Preflight("/api/bookings", AllowedOrigin));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(AllowedOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("POST", response.Headers.GetValues("Access-Control-Allow-Methods").Single());
        Assert.Contains("content-type",
            response.Headers.GetValues("Access-Control-Allow-Headers").Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cors_RejectsOtherOrigins()
    {
        await using WebApplicationFactory<Program> app = WithAllowedOrigin(Factory);
        using HttpClient client = app.CreateClient();

        HttpResponseMessage preflight = await client.SendAsync(Preflight("/api/bookings", "https://evil.example"));
        HttpResponseMessage get = await client.SendAsync(WithOrigin(HttpMethod.Get, "/api/rooms", "https://evil.example"));

        Assert.False(preflight.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(get.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Cors_HeadersAreOnSuccessAndErrorResponses()
    {
        await using WebApplicationFactory<Program> app = WithAllowedOrigin(Factory);
        using HttpClient client = app.CreateClient();
        int roomId = await RoomIdAsync("Room A");

        HttpRequestMessage created = WithOrigin(HttpMethod.Post, "/api/bookings", AllowedOrigin);
        created.Content = JsonContent.Create(new { roomId, bookedBy = "Alice", startUtc = At(10), endUtc = At(11) });
        HttpRequestMessage conflict = WithOrigin(HttpMethod.Post, "/api/bookings", AllowedOrigin);
        conflict.Content = JsonContent.Create(new { roomId, bookedBy = "Bob", startUtc = At(10), endUtc = At(11) });
        HttpRequestMessage invalid = WithOrigin(HttpMethod.Post, "/api/bookings", AllowedOrigin);
        invalid.Content = JsonContent.Create(new { roomId });

        HttpResponseMessage createdResponse = await client.SendAsync(created);
        HttpResponseMessage conflictResponse = await client.SendAsync(conflict);
        HttpResponseMessage invalidResponse = await client.SendAsync(invalid);

        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.All([createdResponse, conflictResponse, invalidResponse], response =>
            Assert.Equal(AllowedOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin"))));
        Assert.Contains("Location", createdResponse.Headers.GetValues("Access-Control-Expose-Headers").Single());
    }

    public static WebApplicationFactory<Program> WithAllowedOrigin(WebApplicationFactory<Program> app) =>
        app.WithWebHostBuilder(builder => builder.UseSetting("Cors:AllowedOrigins:0", AllowedOrigin));

    public static HttpRequestMessage WithOrigin(HttpMethod method, string path, string origin)
    {
        HttpRequestMessage request = new(method, path);
        request.Headers.Add("Origin", origin);
        return request;
    }

    private static HttpRequestMessage Preflight(string path, string origin)
    {
        HttpRequestMessage request = WithOrigin(HttpMethod.Options, path, origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        return request;
    }
}
