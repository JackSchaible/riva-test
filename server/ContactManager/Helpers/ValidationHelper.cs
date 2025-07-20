using System.ComponentModel.DataAnnotations;

namespace ContactManager.Helpers;

public static class ValidationHelper
{
    public static (bool IsValid, List<string> Errors) ValidateModel<T>(T model) where T : class
    {
        List<ValidationResult> validationResults = [];
        ValidationContext context = new(model);

        bool isValid = Validator.TryValidateObject(model, context, validationResults, true);

        List<string> errors = validationResults.Select(vr => vr.ErrorMessage ?? "Unknown validation error").ToList();

        return (isValid, errors);
    }

    public static List<string> GetModelStateErrors(
        Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState) =>
        modelState
            .Where(x => x.Value?.Errors.Count > 0)
            .SelectMany(x => x.Value!.Errors)
            .Select(x => x.ErrorMessage)
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();
}