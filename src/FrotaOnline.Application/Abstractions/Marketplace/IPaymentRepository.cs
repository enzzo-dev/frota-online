using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Abstractions.Marketplace;

public interface IPaymentRepository
{
    Task<bool> ExistsForDeliveryAsync(Guid deliveryId, CancellationToken cancellationToken);

    Task<Payment?> GetByDeliveryIdAsync(Guid deliveryId, CancellationToken cancellationToken);

    Task<Payment?> GetByGatewayIdAsync(string gatewayPaymentId, CancellationToken cancellationToken);

    Task AddAsync(Payment payment, CancellationToken cancellationToken);
}
