using FrotaOnline.Application.Deliveries.Dtos;
using FrotaOnline.Application.Marketplace.Dtos;

namespace FrotaOnline.Application.Abstractions.Queries;

public interface IDeliveryQueries
{
    /// <summary>Deliveries won by the driver, newest first, with stop counters computed by the database.</summary>
    Task<IReadOnlyList<DriverDeliverySummary>> ListByDriverAsync(Guid driverProfileId, CancellationToken cancellationToken);

    /// <summary>
    /// Route of a delivery owned by the driver. <see cref="RouteView.Next"/> returns only the first stop not completed yet.
    /// Null when the delivery does not exist or belongs to another driver.
    /// </summary>
    Task<DeliveryRouteResponse?> GetRouteAsync(
        Guid driverProfileId, Guid deliveryId, RouteView view, CancellationToken cancellationToken);

    /// <summary>Deliveries of the customer, newest first, with driver, vehicle, stop counters and payment status.</summary>
    Task<IReadOnlyList<CustomerDeliverySummary>> ListByCustomerAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>Null when the delivery does not exist or belongs to another customer.</summary>
    Task<CustomerDeliveryDetail?> GetForCustomerAsync(Guid customerId, Guid deliveryId, CancellationToken cancellationToken);
}
