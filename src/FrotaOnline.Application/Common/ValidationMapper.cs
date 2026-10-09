using FluentValidation.Results;

namespace FrotaOnline.Application.Common;

public static class ValidationMapper
{
    public static Error ToError(ValidationResult validation)
    {
        var errors = validation.Errors
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());

        return new Error("validation_error", "One or more validation errors occurred.", ErrorType.Validation, errors);
    }

    public static Error ToError(string field, params string[] messages) =>
        new("validation_error", "One or more validation errors occurred.", ErrorType.Validation,
            new Dictionary<string, string[]> { [field] = messages });
}
