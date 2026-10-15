using FrotaOnline.Application.Common;
using FrotaOnline.Application.Deliveries.Dtos;

namespace FrotaOnline.Application.Deliveries;

/// <summary>What the customer sees about the runs they approved.</summary>
public interface ICustomerDeliveryService
{
    Task<Result<IReadOnlyList<CustomerDeliverySummary>>> ListAsync(Guid customerId, CancellationToken cancellationToken);

    Task<Result<CustomerDeliveryDetail>> GetAsync(Guid customerId, Guid deliveryId, CancellationToken cancellationToken);

    /// <summary>The small payload a screen polls; the full detail is fetched only when its version changes.</summary>
    Task<Result<DeliveryStatusResponse>> GetStatusAsync(Guid customerId, Guid deliveryId, CancellationToken cancellationToken);
}
