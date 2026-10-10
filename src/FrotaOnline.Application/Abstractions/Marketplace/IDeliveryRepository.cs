using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Abstractions.Marketplace;

public interface IDeliveryRepository
{
    Task AddAsync(Delivery delivery, CancellationToken cancellationToken);

    Task AddEventAsync(DeliveryEvent deliveryEvent, CancellationToken cancellationToken);
}
