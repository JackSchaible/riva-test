namespace RoomBooking.Exceptions;

public class RoomNotFoundException(int roomId) : Exception($"Room {roomId} does not exist")
{
    public int RoomId { get; } = roomId;
}
