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
}
