using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FrotaOnline.Api.Extensions;

/// <summary>
/// Two identical requests racing each other can both pass the "does it exist?" check and hit a unique index
/// on save. PostgreSQL then rejects the second one (23505). That is a conflict, not a server failure.
/// </summary>
internal sealed class UniqueViolationExceptionHandler : IExceptionHandler
{
    private const string UniqueViolation = "23505";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateException { InnerException: PostgresException { SqlState: UniqueViolation } })
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "request.duplicate",
                Detail = "The same action was already done by another request. Reload and check the current state."
            },
            cancellationToken);
        return true;
    }
}
