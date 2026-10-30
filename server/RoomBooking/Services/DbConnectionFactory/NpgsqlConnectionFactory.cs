namespace RoomBooking.Services.DbConnectionFactory;

using Npgsql;

public sealed class NpgsqlConnectionFactory(IConfiguration config) : IDbConnectionFactory, IDisposable
{
    private readonly NpgsqlDataSource _dataSource = NpgsqlDataSource.Create(
        config.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found."));

    public NpgsqlConnection CreateConnection() => _dataSource.CreateConnection();

    public void Dispose() => _dataSource.Dispose();
}
