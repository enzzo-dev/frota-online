using FrotaOnline.Application.Common;
using FrotaOnline.Application.Marketplace.Dtos;

namespace FrotaOnline.Application.Marketplace;

/// <summary>Driver side of the marketplace: find open freights, send and withdraw proposals.</summary>
public interface IDriverMarketplaceService
{
    /// <summary>Open freights that match at least one active vehicle of the driver.</summary>
    Task<Result<IReadOnlyList<OpenFreightResponse>>> ListOpenFreightsAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<ProposalResponse>> SubmitProposalAsync(
        Guid userId, Guid freightId, SubmitProposalRequest request, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<ProposalResponse>>> ListProposalsAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result> WithdrawProposalAsync(Guid userId, Guid proposalId, CancellationToken cancellationToken);
}
