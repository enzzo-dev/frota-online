using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Abstractions.Marketplace;

public interface IPaymentRepository
{
    Task<bool> ExistsForDeliveryAsync(Guid deliveryId, CancellationToken cancellationToken);

    Task AddAsync(Payment payment, CancellationToken cancellationToken);
}
