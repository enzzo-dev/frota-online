using FrotaOnline.Application.Common;
using FrotaOnline.Application.Marketplace.Dtos;

namespace FrotaOnline.Application.Marketplace;

/// <summary>Deliveries the driver won and the route to follow, with full addresses.</summary>
public interface IDriverDeliveryService
{
    Task<Result<IReadOnlyList<DriverDeliverySummary>>> ListAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Full returns every stop at once; Next returns only the first stop not completed yet.</summary>
    Task<Result<DeliveryRouteResponse>> GetRouteAsync(
        Guid userId, Guid deliveryId, RouteView view, CancellationToken cancellationToken);
}
