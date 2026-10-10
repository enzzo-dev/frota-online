using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Abstractions.Queries;

/// <summary>
/// Read side of the marketplace. Every method runs in the database and returns finished DTOs:
/// filters, joins, ordering, counts and paging never happen in the application.
/// </summary>
public interface IFreightQueries
{
    /// <summary>Open freights still inside their proposal window for the given vehicle types, newest first (max 100).</summary>
    Task<IReadOnlyList<OpenFreightResponse>> ListOpenAsync(
        DateTime utcNow, IReadOnlyCollection<VehicleType> vehicleTypes, CancellationToken cancellationToken);

    Task<IReadOnlyList<FreightResponse>> ListByCustomerAsync(
        Guid customerId, FreightStatus? status, CancellationToken cancellationToken);

    /// <summary>Null when the freight does not exist or belongs to another customer.</summary>
    Task<FreightResponse?> GetForCustomerAsync(Guid customerId, Guid freightId, CancellationToken cancellationToken);

    /// <summary>
    /// Pending proposals still inside their window, cheapest first, with driver and vehicle already joined.
    /// Null when the freight does not exist or belongs to another customer.
    /// </summary>
    Task<IReadOnlyList<RankedProposalResponse>?> ListRankedProposalsAsync(
        Guid customerId, Guid freightId, DateTime utcNow, CancellationToken cancellationToken);
}
