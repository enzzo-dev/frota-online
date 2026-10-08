using FrotaOnline.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult Handle<T>(Result<T> result, Func<T, IActionResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : ToProblem(result.Error!);

    protected IActionResult HandleEmpty(Result result, Func<IActionResult> onSuccess) =>
        result.IsSuccess ? onSuccess() : ToProblem(result.Error!);

    private IActionResult ToProblem(Error error)
    {
        if (error.Type == ErrorType.Validation && error.ValidationErrors is not null)
        {
            return ValidationProblem(new ValidationProblemDetails(
                error.ValidationErrors.ToDictionary(pair => pair.Key, pair => pair.Value)));
        }

        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        return Problem(title: error.Code, detail: error.Message, statusCode: status);
    }
}
