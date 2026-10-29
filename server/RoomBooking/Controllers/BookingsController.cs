using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RoomBooking.Exceptions;
using RoomBooking.Models.Data;
using RoomBooking.Models.Requests;
using RoomBooking.Models.Responses;
using RoomBooking.Services;

namespace RoomBooking.Controllers;

/// <summary>
/// Controller for viewing, creating, and cancelling room bookings. All times are UTC.
/// </summary>
/// <param name="bookingService">The service for handling booking operations</param>
/// <param name="logger">Logger for logging operations</param>
[ApiController]
[Route("api/bookings")]
[Produces("application/json")]
public class BookingsController(IBookingService bookingService, ILogger<BookingsController> logger)
    : ControllerBase
{
    /// <summary>
    /// Gets active bookings for every room on a UTC day
    /// </summary>
    /// <param name="date">The UTC day, formatted YYYY-MM-DD</param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<BookingResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<List<BookingResponse>>>> GetBookings(
        [FromQuery, BindRequired] DateOnly date)
    {
        List<Booking> bookings = await bookingService.GetForDayAsync(date);

        return Ok(ApiResponse<List<BookingResponse>>.SuccessResult(
            BookingResponse.FromBookings(bookings),
            $"Retrieved {bookings.Count} bookings for {date:yyyy-MM-dd}"
        ));
    }

    /// <summary>
    /// Gets a specific booking by ID, including cancelled bookings
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<BookingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> GetBooking(int id)
    {
        Booking? booking = await bookingService.GetByIdAsync(id);

        if (booking == null)
        {
            return NotFound(ApiResponse<object>.ErrorResult($"Booking {id} was not found"));
        }

        return Ok(ApiResponse<BookingResponse>.SuccessResult(
            BookingResponse.FromBooking(booking),
            "Booking retrieved successfully"
        ));
    }

    /// <summary>
    /// Books a room. Returns 409 Conflict if the room is already booked for any part of the slot.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<BookingResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> CreateBooking(
        [FromBody] CreateBookingRequest? request)
    {
        // Nullable so a body that fails to deserialize reports only its real error, without ASP.NET's
        // implicit "The request field is required." alongside it. A literal JSON null still lands here.
        if (request == null)
        {
            return BadRequest(ApiResponse<object>.ValidationErrorResult(["Request body is required"]));
        }

        // [ApiController] has already rejected invalid requests, so the required values are present.
        try
        {
            Booking booking = await bookingService.CreateAsync(
                request.RoomId!.Value,
                request.BookedBy!.Trim(),
                request.StartUtc!.Value.UtcDateTime,
                request.EndUtc!.Value.UtcDateTime);

            logger.LogInformation("Created booking {Id} for room {RoomId}", booking.Id, booking.RoomId);

            return CreatedAtAction(
                nameof(GetBooking),
                new { id = booking.Id },
                ApiResponse<BookingResponse>.SuccessResult(
                    BookingResponse.FromBooking(booking),
                    "Booking created successfully"
                )
            );
        }
        catch (RoomNotFoundException ex)
        {
            return BadRequest(ApiResponse<object>.ValidationErrorResult([ex.Message]));
        }
        catch (BookingConflictException ex)
        {
            logger.LogInformation("Rejected overlapping booking for room {RoomId}: {Message}",
                request.RoomId, ex.Message);
            return Conflict(ApiResponse<object>.ErrorResult(ex.Message));
        }
    }

    /// <summary>
    /// Cancels a booking so its time becomes available again. Cancelling twice succeeds.
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<BookingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> CancelBooking(int id)
    {
        Booking? booking = await bookingService.CancelAsync(id);

        if (booking == null)
        {
            return NotFound(ApiResponse<object>.ErrorResult($"Booking {id} was not found"));
        }

        logger.LogInformation("Cancelled booking {Id}", id);

        return Ok(ApiResponse<BookingResponse>.SuccessResult(
            BookingResponse.FromBooking(booking),
            "Booking cancelled successfully"
        ));
    }
}
