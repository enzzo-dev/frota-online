using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Abstractions.Marketplace;

public interface IProposalRepository
{
    Task<Proposal?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Proposal>> ListPendingByFreightAsync(Guid freightRequestId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Proposal>> ListByDriverAsync(Guid driverProfileId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Proposal>> ListPendingDueAsync(DateTime utcNow, CancellationToken cancellationToken);

    /// <summary>Number of pending proposals per freight id (ids without proposals are absent).</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountPendingByFreightAsync(
        IReadOnlyCollection<Guid> freightRequestIds, CancellationToken cancellationToken);

    Task<bool> HasPendingAsync(Guid freightRequestId, Guid driverProfileId, CancellationToken cancellationToken);

    Task AddAsync(Proposal proposal, CancellationToken cancellationToken);
}
