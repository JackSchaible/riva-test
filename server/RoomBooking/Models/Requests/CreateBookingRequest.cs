using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RoomBooking.Json;

namespace RoomBooking.Models.Requests;

public class CreateBookingRequest : IValidatableObject
{
    public const int MaxNameLength = 100;

    [Required(ErrorMessage = "Room is required")]
    public int? RoomId { get; set; }

    [Required(ErrorMessage = "Name is required")]
    [StringLength(MaxNameLength, ErrorMessage = "Name must not exceed 100 characters")]
    public string? BookedBy { get; set; }

    [Required(ErrorMessage = "Start time is required")]
    [JsonConverter(typeof(ExplicitOffsetDateTimeOffsetConverter))]
    public DateTimeOffset? StartUtc { get; set; }

    [Required(ErrorMessage = "End time is required")]
    [JsonConverter(typeof(ExplicitOffsetDateTimeOffsetConverter))]
    public DateTimeOffset? EndUtc { get; set; }

    /// <summary>
    /// Enforces the booking time rules: UTC only, end after start, and within the start's UTC day
    /// (ending at 00:00 UTC the following day at the latest).
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartUtc is not { } start || EndUtc is not { } end)
        {
            yield break;
        }

        bool offsetsValid = true;

        if (start.Offset != TimeSpan.Zero)
        {
            offsetsValid = false;
            yield return new ValidationResult("Start time must be in UTC (offset +00:00 or Z)", [nameof(StartUtc)]);
        }

        if (end.Offset != TimeSpan.Zero)
        {
            offsetsValid = false;
            yield return new ValidationResult("End time must be in UTC (offset +00:00 or Z)", [nameof(EndUtc)]);
        }

        if (!offsetsValid)
        {
            yield break;
        }

        if (end <= start)
        {
            yield return new ValidationResult("End time must be after start time", [nameof(EndUtc)]);
        }
        else if (end > start.UtcDateTime.Date.AddDays(1))
        {
            yield return new ValidationResult(
                "Booking must be within a single UTC day (ending no later than 00:00 UTC the next day)",
                [nameof(EndUtc)]);
        }
    }
}
