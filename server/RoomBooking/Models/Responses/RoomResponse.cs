using RoomBooking.Models.Data;

namespace RoomBooking.Models.Responses;

public class RoomResponse
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public static RoomResponse FromRoom(Room room) => new() { Id = room.Id, Name = room.Name };

    public static List<RoomResponse> FromRooms(List<Room> rooms) => rooms.Select(FromRoom).ToList();
}
