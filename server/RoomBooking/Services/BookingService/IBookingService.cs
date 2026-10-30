namespace RoomBooking.Services;

using Models.Data;

public interface IBookingService
{
    /// <summary>Active bookings for every room that start on the given UTC day.</summary>
    Task<List<Booking>> GetForDayAsync(DateOnly day);

    /// <summary>A booking by ID, including cancelled bookings.</summary>
    Task<Booking?> GetByIdAsync(int id);

    /// <exception cref="Exceptions.BookingConflictException">The slot overlaps an active booking.</exception>
    /// <exception cref="Exceptions.RoomNotFoundException">The room does not exist.</exception>
    Task<Booking> CreateAsync(int roomId, string bookedBy, DateTime startUtc, DateTime endUtc);

    /// <summary>Cancels a booking. Returns null if it does not exist; cancelling twice is a no-op.</summary>
    Task<Booking?> CancelAsync(int id);
}
