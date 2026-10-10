using FrotaOnline.Application.Common;
using FrotaOnline.Application.Marketplace.Dtos;

namespace FrotaOnline.Application.Marketplace;

/// <summary>Customer side: compare the proposals of a freight and pick the winner.</summary>
public interface IProposalSelectionService
{
    /// <summary>Pending proposals ordered by price, then by arrival.</summary>
    Task<Result<IReadOnlyList<RankedProposalResponse>>> ListRankedAsync(
        Guid customerId, Guid freightId, CancellationToken cancellationToken);

    /// <summary>
    /// Approves one proposal: creates the delivery, reserves the driver's agenda, rejects the other
    /// proposals and closes the freight, all in a single save.
    /// </summary>
    Task<Result<ApprovedDeliveryResponse>> ApproveAsync(
        Guid customerId, Guid freightId, Guid proposalId, CancellationToken cancellationToken);
}
