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

    // Stops and their items always travel with the freight.
    private IQueryable<FreightRequest> WithStops() =>
        _db.FreightRequests.Include(f => f.Stops).ThenInclude(s => s.Items).AsSplitQuery();

    public Task<FreightRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        WithStops().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public Task<int> ExpireDueAsync(DateTime utcNow, CancellationToken cancellationToken) =>
        _db.FreightRequests
            .Where(f => f.Status == FreightStatus.Open && f.ProposalDeadline <= utcNow)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(f => f.Status, FreightStatus.Expired)
                    .SetProperty(f => f.UpdatedAt, utcNow),
                cancellationToken);

    public async Task AddAsync(FreightRequest freight, CancellationToken cancellationToken) =>
        await _db.FreightRequests.AddAsync(freight, cancellationToken);
}
