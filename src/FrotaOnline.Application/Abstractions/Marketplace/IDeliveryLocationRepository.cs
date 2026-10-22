using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Abstractions.Marketplace;

public interface IDeliveryLocationRepository
{
    Task<DeliveryLocation?> GetByDeliveryIdAsync(Guid deliveryId, CancellationToken cancellationToken);

    Task AddAsync(DeliveryLocation location, CancellationToken cancellationToken);

    void Remove(DeliveryLocation location);

    Task<bool> TryRecordForDriverAsync(Guid driverUserId, DeliveryLocation location, CancellationToken cancellationToken);
}
