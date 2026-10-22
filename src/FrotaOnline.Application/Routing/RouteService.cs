using FluentValidation;
using FrotaOnline.Application.Abstractions.Addresses;
using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Addresses;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Drivers;
using FrotaOnline.Application.Marketplace;
using FrotaOnline.Application.Routing.Dtos;
using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Routing;

public sealed class RouteService : IRouteService
{
    private readonly IAddressRepository _addresses;
    private readonly IFreightRepository _freights;
    private readonly IDeliveryRepository _deliveries;
    private readonly IDriverRepository _drivers;
    private readonly IRoutePlanner _planner;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<RoutePreviewRequest> _validator;

    public RouteService(
        IAddressRepository addresses,
        IFreightRepository freights,
        IDeliveryRepository deliveries,
        IDriverRepository drivers,
        IRoutePlanner planner,
        IUnitOfWork unitOfWork,
        IValidator<RoutePreviewRequest> validator)
    {
        _addresses = addresses;
        _freights = freights;
        _deliveries = deliveries;
        _drivers = drivers;
        _planner = planner;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<Result<RouteMapResponse>> PreviewAsync(
        Guid customerId, RoutePreviewRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<RouteMapResponse>(ValidationMapper.ToError(validation));
        }

        var found = (await _addresses.ListByIdsAsync(request.AddressIds, cancellationToken)).ToDictionary(a => a.Id);
        if (request.AddressIds.Any(id => !found.TryGetValue(id, out var a) || a.OwnerUserId != customerId || a.IsDeleted))
        {
            return Result.Failure<RouteMapResponse>(AddressErrors.NotFound);
        }

        var stops = request.AddressIds.Select(id => new RoutedStop(found[id], null)).ToList();
        return await PlanAsync(stops, cancellationToken);
    }

    public async Task<Result<RouteMapResponse>> ForFreightAsync(
        Guid customerId, Guid freightId, CancellationToken cancellationToken)
    {
        var freight = await _freights.GetByIdAsync(freightId, cancellationToken);
        if (freight is null || freight.CustomerId != customerId)
        {
            return Result.Failure<RouteMapResponse>(MarketplaceErrors.FreightNotFound);
        }

        return await PlanFreightAsync(freight, cancellationToken);
    }

    public async Task<Result<RouteMapResponse>> ForDriverDeliveryAsync(
        Guid userId, Guid deliveryId, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<RouteMapResponse>(DriverErrors.ProfileNotFound);
        }

        var delivery = await _deliveries.GetByIdAsync(deliveryId, cancellationToken);
        if (delivery is null || delivery.DriverProfileId != profile.Id)
        {
            return Result.Failure<RouteMapResponse>(MarketplaceErrors.DeliveryNotFound);
        }

        var freight = await _freights.GetByIdAsync(delivery.FreightRequestId, cancellationToken);
        return freight is null
            ? Result.Failure<RouteMapResponse>(MarketplaceErrors.FreightNotFound)
            : await PlanFreightAsync(freight, cancellationToken);
    }

    private async Task<Result<RouteMapResponse>> PlanFreightAsync(FreightRequest freight, CancellationToken cancellationToken)
    {
        var ordered = freight.Stops.OrderBy(s => s.Sequence).ToList();
        var found = (await _addresses.ListByIdsAsync(ordered.Select(s => s.AddressId).ToList(), cancellationToken))
            .ToDictionary(a => a.Id);

        var stops = ordered.Select(s => new RoutedStop(found[s.AddressId], s.Type)).ToList();
        if (freight is { RouteDistanceMeters: { } meters, RouteDurationSeconds: { } seconds, RoutePolyline: { } polyline }
            && _planner.Restore(stops, meters, seconds, polyline) is { } stored)
        {
            return Result.Success(stored);
        }

        var planned = await PlanAsync(stops, cancellationToken);
        if (planned.IsSuccess && planned.Value is { DistanceMeters: { } distance, DurationSeconds: { } duration, Path.Count: > 0 })
        {
            freight.SetRoute(distance, duration, PolylineCodec.Encode(planned.Value.Path));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return planned;
    }

    private async Task<Result<RouteMapResponse>> PlanAsync(IReadOnlyList<RoutedStop> stops, CancellationToken cancellationToken)
    {
        var map = await _planner.PlanAsync(stops, cancellationToken);
        return map is null
            ? Result.Failure<RouteMapResponse>(RoutingErrors.MissingCoordinates(
                stops.Where(s => s.Address.Latitude is null || s.Address.Longitude is null).Select(s => s.Address.Label)))
            : Result.Success(map);
    }
}
