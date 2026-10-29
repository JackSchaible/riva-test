namespace RoomBooking.Services;

using Models.Data;

public interface IRoomService
{
    Task<List<Room>> GetAllAsync();
}
