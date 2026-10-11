using FrotaOnline.Application.Common;
using FrotaOnline.Application.Routing.Dtos;

namespace FrotaOnline.Application.Routing;

public interface IRouteService
{
    /// <summary>The customer sees the route as soon as the addresses are chosen, before creating the freight.</summary>
    Task<Result<RouteMapResponse>> PreviewAsync(Guid customerId, RoutePreviewRequest request, CancellationToken cancellationToken);

    /// <summary>Route of a freight the customer already created.</summary>
    Task<Result<RouteMapResponse>> ForFreightAsync(Guid customerId, Guid freightId, CancellationToken cancellationToken);

    /// <summary>Exact route of a delivery the driver won. Before the win the driver only gets the approximate view.</summary>
    Task<Result<RouteMapResponse>> ForDriverDeliveryAsync(Guid userId, Guid deliveryId, CancellationToken cancellationToken);
}
