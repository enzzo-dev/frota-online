using FrotaOnline.Application.Common;
using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Marketplace;

/// <summary>Customer side of the marketplace: publish, follow and cancel freight requests.</summary>
public interface IFreightService
{
    Task<Result<FreightResponse>> CreateAsync(Guid customerId, CreateFreightRequest request, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<FreightSimpleResponse>>> ListAsync(Guid customerId, FreightStatus? status, CancellationToken cancellationToken);

    Task<Result<FreightResponse>> GetAsync(Guid customerId, Guid freightId, CancellationToken cancellationToken);

    /// <summary>Cancels an open freight and rejects its pending proposals.</summary>
    Task<Result> CancelAsync(Guid customerId, Guid freightId, CancellationToken cancellationToken);
}
