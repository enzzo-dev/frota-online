using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class ProposalRepository : IProposalRepository
{
    private readonly AppDbContext _db;

    public ProposalRepository(AppDbContext db) => _db = db;

    public Task<Proposal?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Proposals.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Proposal>> ListPendingByFreightAsync(Guid freightRequestId, CancellationToken cancellationToken) =>
        await _db.Proposals
            .Where(p => p.FreightRequestId == freightRequestId && p.Status == ProposalStatus.Pending)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Proposal>> ListByDriverAsync(Guid driverProfileId, CancellationToken cancellationToken) =>
        await _db.Proposals
            .AsNoTracking()
            .Where(p => p.DriverProfileId == driverProfileId)
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ExpiredProposal>> ExpireDueAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        var changed = await _db.Proposals
            .Where(p => p.Status == ProposalStatus.Pending && p.ExpiresAt <= utcNow)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(p => p.Status, ProposalStatus.Expired)
                    .SetProperty(p => p.UpdatedAt, utcNow),
                cancellationToken);

        if (changed == 0)
        {
            return Array.Empty<ExpiredProposal>();
        }

        // The UPDATE stamped these rows with this pass's timestamp. PostgreSQL keeps microseconds and .NET ticks are
        // finer, so compare with a one-millisecond margin instead of equality.
        var stamp = utcNow.AddMilliseconds(-1);

        return await _db.Proposals
            .AsNoTracking()
            .Where(p => p.Status == ProposalStatus.Expired && p.UpdatedAt >= stamp)
            .Select(p => new ExpiredProposal(p.Id, p.DriverProfileId))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasPendingAsync(Guid freightRequestId, Guid driverProfileId, CancellationToken cancellationToken) =>
        _db.Proposals.AnyAsync(
            p => p.FreightRequestId == freightRequestId && p.DriverProfileId == driverProfileId && p.Status == ProposalStatus.Pending,
            cancellationToken);

    public async Task AddAsync(Proposal proposal, CancellationToken cancellationToken) =>
        await _db.Proposals.AddAsync(proposal, cancellationToken);
}
