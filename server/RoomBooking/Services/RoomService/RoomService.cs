namespace RoomBooking.Services;

using Dapper;
using DbConnectionFactory;
using Models.Data;

public class RoomService(IDbConnectionFactory connectionFactory) : IRoomService
{
    private const string GetAllQuery = "SELECT id AS Id, name AS Name FROM rooms ORDER BY name";

    public async Task<List<Room>> GetAllAsync()
    {
        await using var connection = connectionFactory.CreateConnection();
        IEnumerable<Room> rooms = await connection.QueryAsync<Room>(GetAllQuery);
        return rooms.ToList();
    }
}
