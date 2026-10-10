using FrotaOnline.Application.Terms;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Queries;

/// <summary>
/// "Is there an active term this user has not accepted?" answered by one NOT EXISTS query,
/// instead of loading every active term and every consent and comparing them in memory.
/// </summary>
internal sealed class SqlTermAcceptanceChecker : ITermAcceptanceChecker
{
    private readonly AppDbContext _db;

    public SqlTermAcceptanceChecker(AppDbContext db) => _db = db;

    public async Task<bool> HasAcceptedAllActiveAsync(Guid userId, CancellationToken cancellationToken) =>
        !await _db.TermDocuments
            .AsNoTracking()
            .AnyAsync(
                t => t.IsActive && !_db.UserConsents.Any(c => c.UserId == userId && c.TermDocumentId == t.Id),
                cancellationToken);
}
