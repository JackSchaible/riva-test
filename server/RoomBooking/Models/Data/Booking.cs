namespace RoomBooking.Models.Data;

/// <summary>
/// A booking row. Timestamps are UTC <see cref="DateTime"/> values, as read from timestamptz by Npgsql.
/// </summary>
public class Booking
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public required string RoomName { get; set; }
    public required string BookedBy { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}
