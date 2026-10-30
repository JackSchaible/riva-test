using Microsoft.AspNetCore.Mvc;
using RoomBooking.Models.Data;
using RoomBooking.Models.Responses;
using RoomBooking.Services;

namespace RoomBooking.Controllers;

/// <summary>
/// Controller for listing meeting rooms
/// </summary>
[ApiController]
[Route("api/rooms")]
[Produces("application/json")]
public class RoomsController(IRoomService roomService) : ControllerBase
{
    /// <summary>
    /// Gets all rooms
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<RoomResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<RoomResponse>>>> GetRooms()
    {
        List<Room> rooms = await roomService.GetAllAsync();

        return Ok(ApiResponse<List<RoomResponse>>.SuccessResult(
            RoomResponse.FromRooms(rooms),
            $"Retrieved {rooms.Count} rooms successfully"
        ));
    }
}
