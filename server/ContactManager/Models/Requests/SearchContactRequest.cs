using System.ComponentModel.DataAnnotations;

namespace ContactManager.Models.Data.Requests;

public class SearchContactRequest
{
    [StringLength(100, ErrorMessage = "Search query must not exceed 100 characters")]
    public string? Query { get; set; }

    public string GetSafeQuery() => Query?.Trim() ?? string.Empty;
}
