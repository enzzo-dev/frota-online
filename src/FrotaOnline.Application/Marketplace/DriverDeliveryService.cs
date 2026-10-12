using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Drivers;
using FrotaOnline.Application.Marketplace.Dtos;

namespace FrotaOnline.Application.Marketplace;

public sealed class DriverDeliveryService : IDriverDeliveryService
{
    private readonly IDriverRepository _drivers;
    private readonly IDeliveryQueries _queries;

    public DriverDeliveryService(IDriverRepository drivers, IDeliveryQueries queries)
    {
        _drivers = drivers;
        _queries = queries;
    }

    public async Task<Result<IReadOnlyList<DriverDeliverySummary>>> ListAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<IReadOnlyList<DriverDeliverySummary>>(DriverErrors.ProfileNotFound);
        }

        var rows = await _queries.ListByDriverAsync(profile.Id, cancellationToken);
        return Result.Success(rows);
    }

    public async Task<Result<DriverDeliverySummary>> GetAsync(
        Guid userId, Guid deliveryId, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<DriverDeliverySummary>(DriverErrors.ProfileNotFound);
        }

        var delivery = await _queries.GetSummaryByDriverAsync(profile.Id, deliveryId, cancellationToken);
        return delivery is null
            ? Result.Failure<DriverDeliverySummary>(MarketplaceErrors.DeliveryNotFound)
            : Result.Success(delivery);
    }

    public async Task<Result<DeliveryRouteResponse>> GetRouteAsync(
        Guid userId, Guid deliveryId, RouteView view, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<DeliveryRouteResponse>(DriverErrors.ProfileNotFound);
        }

        var route = await _queries.GetRouteAsync(profile.Id, deliveryId, view, cancellationToken);
        return route is null
            ? Result.Failure<DeliveryRouteResponse>(MarketplaceErrors.DeliveryNotFound)
            : Result.Success(route);
    }
}
