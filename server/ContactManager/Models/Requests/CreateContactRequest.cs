using System.ComponentModel.DataAnnotations;

namespace ContactManager.Models.Data.Requests;

public class CreateContactRequest
{
    [Required(ErrorMessage = "First name is required")]
    [StringLength(64, MinimumLength = 1, ErrorMessage = "First name must be between 1 and 64 characters")]
    public required string FirstName { get; set; }

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(64, MinimumLength = 1, ErrorMessage = "Last name must be between 1 and 64 characters")]
    public required string LastName { get; set; }

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Please provide a valid email address")]
    [StringLength(256, ErrorMessage = "Email must not exceed 256 characters")]
    public required string Email { get; set; }

    [Required(ErrorMessage = "Phone number is required")]
    [Phone(ErrorMessage = "Please provide a valid phone number")]
    [StringLength(256, MinimumLength = 10, ErrorMessage = "Phone number must be between 10 and 256 characters")]
    public required string Phone { get; set; }

    public Contact ToContact() =>
        new()
        {
            FirstName = FirstName.Trim(),
            LastName = LastName.Trim(),
            Email = Email.Trim().ToLowerInvariant(),
            Phone = Phone.Trim()
        };
}
