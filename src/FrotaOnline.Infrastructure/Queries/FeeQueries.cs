using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Deliveries.Dtos;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Queries;

internal sealed class FeeQueries : IFeeQueries
{
    private const int MaxEntries = 200;

    private readonly AppDbContext _db;

    public FeeQueries(AppDbContext db) => _db = db;

    public async Task<FeeStatementResponse> GetStatementAsync(Guid driverProfileId, CancellationToken cancellationToken)
    {
        var ledger = _db.FeeLedgers.AsNoTracking().Where(e => e.DriverProfileId == driverProfileId);

        // Totals are a GROUP BY in SQL; the entries are a separate TOP query.
        var totals = await ledger
            .GroupBy(e => e.EntryType)
            .Select(g => new { Type = g.Key, Total = g.Sum(e => e.Amount) })
            .ToListAsync(cancellationToken);

        var accrued = totals.Where(t => t.Type == FeeEntryType.FeeAccrued).Sum(t => t.Total);
        var settled = totals.Where(t => t.Type == FeeEntryType.FeeSettled).Sum(t => t.Total);

        var rows = await ledger
            .OrderByDescending(e => e.CreatedAt)
            .ThenBy(e => e.Id)
            .Take(MaxEntries)
            .Select(e => new { e.Id, e.DeliveryId, e.EntryType, e.Amount, e.Description, e.CreatedAt })
            .ToListAsync(cancellationToken);

        var entries = rows
            .Select(r => new FeeEntryResponse(r.Id, r.DeliveryId, r.EntryType, r.Amount, r.Description, r.CreatedAt))
            .ToList();

        return new FeeStatementResponse(accrued - settled, accrued, settled, entries);
    }
}
