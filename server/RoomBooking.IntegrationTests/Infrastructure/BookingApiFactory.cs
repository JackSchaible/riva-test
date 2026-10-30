using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace RoomBooking.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the API in memory against a throwaway PostgreSQL container shared by every test.
/// The app's startup migrations create the schema and seed the rooms.
/// </summary>
public sealed class BookingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public string ConnectionString => _database.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development", so the sample bookings are not seeded.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
    }

    public Task InitializeAsync() => _database.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<BookingApiFactory>
{
    public const string Name = "API";
}
