using FrotaOnline.Application.Common;
using FrotaOnline.Application.Deliveries.Dtos;

namespace FrotaOnline.Application.Deliveries;

public interface IDeliveryProgressService
{
    /// <summary>The driver leaves for the first pickup (Scheduled to ToPickup).</summary>
    Task<Result<DeliveryProgressResponse>> StartAsync(Guid userId, Guid deliveryId, CancellationToken cancellationToken);

    /// <summary>Completes the next stop in order and moves the delivery forward. The last stop delivers it.</summary>
    Task<Result<DeliveryProgressResponse>> CompleteStopAsync(
        Guid userId, Guid deliveryId, Guid stopId, CompleteStopRequest request, CancellationToken cancellationToken);

    Task<Result<DeliveryProgressResponse>> ReportIncidentAsync(
        Guid userId, Guid deliveryId, ReportIncidentRequest request, CancellationToken cancellationToken);

    Task<Result<DeliveryProgressResponse>> CancelAsDriverAsync(
        Guid userId, Guid deliveryId, CancelDeliveryRequest request, CancellationToken cancellationToken);

    /// <summary>The customer can cancel only before the cargo is picked up.</summary>
    Task<Result<DeliveryProgressResponse>> CancelAsCustomerAsync(
        Guid customerId, Guid deliveryId, CancelDeliveryRequest request, CancellationToken cancellationToken);
}
