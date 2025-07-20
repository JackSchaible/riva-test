namespace ContactManager.Models.Data.Responses;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }

    public static ApiResponse<T> SuccessResult(T? data, string? message = null) =>
        new()
        {
            Success = true,
            Data = data,
            Message = message
        };

    public static ApiResponse<T> ErrorResult(string message, List<string>? errors = null) =>
        new()
        {
            Success = false,
            Message = message,
            Errors = errors
        };

    public static ApiResponse<T> ValidationErrorResult(List<string> errors) =>
        new()
        {
            Success = false,
            Message = "Validation failed",
            Errors = errors
        };
}
