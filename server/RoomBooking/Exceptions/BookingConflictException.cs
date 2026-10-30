namespace RoomBooking.Exceptions;

/// <summary>
/// Thrown when a booking would overlap an active booking for the same room.
/// </summary>
public class BookingConflictException(string message) : Exception(message);
