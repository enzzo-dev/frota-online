using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Abstractions.Queries;

/// <summary>
/// Read side of the marketplace. Every method runs in the database and returns finished DTOs:
/// filters, joins, ordering, counts and paging never happen in the application.
/// </summary>
public interface IFreightQueries
{
    /// <summary>
    /// Open freights still inside their proposal window that one of the driver's vehicles can carry (same type and enough
    /// capacity, given as the largest capacity per type) and that the driver has no pending proposal on, newest first (max 100).
    /// </summary>
    Task<IReadOnlyList<OpenFreightResponse>> ListOpenAsync(
        DateTime utcNow,
        Guid driverProfileId,
        IReadOnlyDictionary<VehicleType, decimal> maxCapacityByType,
        CancellationToken cancellationToken);

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
