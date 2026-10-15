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

        return new Error("validation_error", "Um ou mais erros de validação ocorreram.", ErrorType.Validation, errors);
    }

    public static Error ToError(string field, params string[] messages) =>
        new("validation_error", "Um ou mais erros de validação ocorreram.", ErrorType.Validation,
            new Dictionary<string, string[]> { [field] = messages });
}
