using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class FreightRepository : IFreightRepository
{
    private readonly AppDbContext _db;

    public FreightRepository(AppDbContext db) => _db = db;

    // Stops travel with the freight. Items do not: services never read them from the aggregate (the read side
    // queries them straight into DTOs), so loading them would cost a third statement on every call.
    private IQueryable<FreightRequest> WithStops() =>
        _db.FreightRequests.Include(f => f.Stops);

    public Task<FreightRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        WithStops().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ExpiredFreight>> ExpireDueAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        var changed = await _db.FreightRequests
            .Where(f => f.Status == FreightStatus.Open && f.ProposalDeadline <= utcNow)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(f => f.Status, FreightStatus.Expired)
                    .SetProperty(f => f.UpdatedAt, utcNow),
                cancellationToken);

        if (changed == 0)
        {
            return Array.Empty<ExpiredFreight>();
        }

        // The UPDATE stamped these rows with this pass's timestamp. PostgreSQL keeps microseconds and .NET ticks are
        // finer, so compare with a one-millisecond margin instead of equality.
        var stamp = utcNow.AddMilliseconds(-1);

        return await _db.FreightRequests
            .AsNoTracking()
            .Where(f => f.Status == FreightStatus.Expired && f.UpdatedAt >= stamp)
            .Select(f => new ExpiredFreight(f.Id, f.CustomerId))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(FreightRequest freight, CancellationToken cancellationToken) =>
        await _db.FreightRequests.AddAsync(freight, cancellationToken);
}
