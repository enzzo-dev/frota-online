using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Abstractions.Marketplace;

public interface IDeliveryRepository
{
    Task<Delivery?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Deliveries won by the driver, newest first.</summary>
    Task<IReadOnlyList<Delivery>> ListByDriverAsync(Guid driverProfileId, CancellationToken cancellationToken);

    Task AddAsync(Delivery delivery, CancellationToken cancellationToken);

    Task AddEventAsync(DeliveryEvent deliveryEvent, CancellationToken cancellationToken);
}
