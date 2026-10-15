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
    private const string ForeignKeyViolation = "23503";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateException { InnerException: PostgresException postgres })
        {
            return false;
        }

        // 23503: another request started using a row (for example a new freight at an address) while this one deleted it.
        var (title, detail) = postgres.SqlState switch
        {
            UniqueViolation => ("request.duplicate", "A mesma ação já foi feita por outra requisição. Atualize e confira o estado atual."),
            ForeignKeyViolation => ("request.conflict", "Algo mudou enquanto a requisição era processada. Atualize e tente novamente."),
            _ => ((string?)null, (string?)null)
        };

        if (title is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = title, Detail = detail },
            cancellationToken);
        return true;
    }
}
