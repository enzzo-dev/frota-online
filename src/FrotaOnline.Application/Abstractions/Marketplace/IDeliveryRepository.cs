using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Abstractions.Marketplace;

public interface IDeliveryRepository
{
    Task<Delivery?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Delivery?> GetByFreightRequestIdAsync(Guid freightRequestId, CancellationToken cancellationToken);

    /// <summary>How many freights the driver has taken (deliveries that were not cancelled). Used for the free freight without a plan.</summary>
    Task<int> CountTakenByDriverAsync(Guid driverProfileId, CancellationToken cancellationToken);

    Task AddAsync(Delivery delivery, CancellationToken cancellationToken);

    Task AddEventAsync(DeliveryEvent deliveryEvent, CancellationToken cancellationToken);
}
