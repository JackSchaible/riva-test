using RoomBooking.Models.Data;

namespace RoomBooking.Models.Responses;

public class BookingResponse
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public required string RoomName { get; set; }
    public required string BookedBy { get; set; }
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    public static BookingResponse FromBooking(Booking booking) =>
        new()
        {
            Id = booking.Id,
            RoomId = booking.RoomId,
            RoomName = booking.RoomName,
            BookedBy = booking.BookedBy,
            StartUtc = ToUtc(booking.StartUtc),
            EndUtc = ToUtc(booking.EndUtc),
            CreatedAt = ToUtc(booking.CreatedAt),
            CancelledAt = booking.CancelledAt is { } cancelledAt ? ToUtc(cancelledAt) : null
        };

    public static List<BookingResponse> FromBookings(List<Booking> bookings) =>
        bookings.Select(FromBooking).ToList();

    private static DateTimeOffset ToUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);
}
