using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Abstractions.Marketplace;

public interface IFreightRepository
{
    Task<FreightRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<FreightRequest>> ListByCustomerAsync(
        Guid customerId, FreightStatus? status, CancellationToken cancellationToken);

    /// <summary>Open freights still inside their proposal window for the given vehicle types, newest first (max 100).</summary>
    Task<IReadOnlyList<FreightRequest>> ListOpenAsync(
        DateTime utcNow, IReadOnlyCollection<VehicleType> vehicleTypes, CancellationToken cancellationToken);

    /// <summary>Open freights whose proposal window has already ended.</summary>
    Task<IReadOnlyList<FreightRequest>> ListOpenDueAsync(DateTime utcNow, CancellationToken cancellationToken);

    Task AddAsync(FreightRequest freight, CancellationToken cancellationToken);
}
