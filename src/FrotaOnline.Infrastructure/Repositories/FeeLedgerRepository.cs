using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class FeeLedgerRepository : IFeeLedgerRepository
{
    private readonly AppDbContext _db;

    public FeeLedgerRepository(AppDbContext db) => _db = db;

    // One SUM in the database; nothing is loaded.
    public async Task<decimal> GetBalanceAsync(Guid driverProfileId, CancellationToken cancellationToken) =>
        await _db.FeeLedgers
            .Where(e => e.DriverProfileId == driverProfileId)
            .SumAsync(e => e.EntryType == FeeEntryType.FeeAccrued ? e.Amount : -e.Amount, cancellationToken);

    public async Task AddAsync(FeeLedger entry, CancellationToken cancellationToken) =>
        await _db.FeeLedgers.AddAsync(entry, cancellationToken);
}
