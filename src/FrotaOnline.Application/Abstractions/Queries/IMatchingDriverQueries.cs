namespace FrotaOnline.Application.Abstractions.Queries;

/// <summary>What a new freight needs from a driver, as seen by the matching query.</summary>
public sealed record FreightMatchCriteria(
    Guid CustomerId,
    Domain.Enums.VehicleType VehicleType,
    decimal CargoWeightKg,
    DateTime StartsAt,
    DateTime EndsAt);

public interface IMatchingDriverQueries
{
    /// <summary>
    /// User ids of the drivers a new freight is worth telling: approved (and not the customer who posted it), with an active vehicle of the required type
    /// and enough capacity, free in the agenda during the run, and with the alert switched on. One set-based query,
    /// capped at <paramref name="limit"/> (the people who registered first are kept; there is no distance filter yet).
    /// </summary>
    Task<IReadOnlyList<Guid>> ListUserIdsForNewFreightAsync(
        FreightMatchCriteria criteria, int limit, CancellationToken cancellationToken);
}
