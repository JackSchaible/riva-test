using System.ComponentModel.DataAnnotations;

namespace ContactManager.Models.Data.Requests;

public class SearchContactRequest
{
    [Required(ErrorMessage = "Search query is required")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Search query must be between 3 and 100 characters")]
    public string? Query { get; set; }

    public string GetSafeQuery() => Query?.Trim() ?? string.Empty;
}
