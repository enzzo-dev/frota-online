using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Abstractions.Marketplace;

public sealed record ExpiredProposal(Guid ProposalId, Guid DriverProfileId);

public interface IProposalRepository
{
    Task<Proposal?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Proposal>> ListPendingByFreightAsync(Guid freightRequestId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Proposal>> ListByDriverAsync(Guid driverProfileId, CancellationToken cancellationToken);

    /// <summary>
    /// Moves every pending proposal whose window ended to Expired with one UPDATE statement and returns
    /// the proposals that changed (so their drivers can be told). Bypasses the change tracker on purpose.
    /// </summary>
    Task<IReadOnlyList<ExpiredProposal>> ExpireDueAsync(DateTime utcNow, CancellationToken cancellationToken);

    Task<bool> HasPendingAsync(Guid freightRequestId, Guid driverProfileId, CancellationToken cancellationToken);

    Task AddAsync(Proposal proposal, CancellationToken cancellationToken);
}
