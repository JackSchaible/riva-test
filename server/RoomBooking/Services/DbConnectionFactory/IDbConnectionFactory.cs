namespace RoomBooking.Services.DbConnectionFactory;

using Npgsql;

public interface IDbConnectionFactory
{
    NpgsqlConnection CreateConnection();
}
