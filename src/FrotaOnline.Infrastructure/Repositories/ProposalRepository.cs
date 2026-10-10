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
            .Where(p => p.DriverProfileId == driverProfileId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Proposal>> ListPendingDueAsync(DateTime utcNow, CancellationToken cancellationToken) =>
        await _db.Proposals
            .Where(p => p.Status == ProposalStatus.Pending && p.ExpiresAt <= utcNow)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> CountPendingByFreightAsync(
        IReadOnlyCollection<Guid> freightRequestIds, CancellationToken cancellationToken) =>
        await _db.Proposals
            .Where(p => p.Status == ProposalStatus.Pending && freightRequestIds.Contains(p.FreightRequestId))
            .GroupBy(p => p.FreightRequestId)
            .Select(g => new { FreightRequestId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FreightRequestId, x => x.Count, cancellationToken);

    public Task<bool> HasPendingAsync(Guid freightRequestId, Guid driverProfileId, CancellationToken cancellationToken) =>
        _db.Proposals.AnyAsync(
            p => p.FreightRequestId == freightRequestId && p.DriverProfileId == driverProfileId && p.Status == ProposalStatus.Pending,
            cancellationToken);

    public async Task AddAsync(Proposal proposal, CancellationToken cancellationToken) =>
        await _db.Proposals.AddAsync(proposal, cancellationToken);
}
