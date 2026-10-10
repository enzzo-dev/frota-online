using FrotaOnline.Application.Abstractions.Addresses;
using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Addresses;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Drivers;
using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Marketplace;

public sealed class DriverDeliveryService : IDriverDeliveryService
{
    private readonly IDriverRepository _drivers;
    private readonly IDeliveryRepository _deliveries;
    private readonly IFreightRepository _freights;
    private readonly IAddressRepository _addresses;

    public DriverDeliveryService(
        IDriverRepository drivers,
        IDeliveryRepository deliveries,
        IFreightRepository freights,
        IAddressRepository addresses)
    {
        _drivers = drivers;
        _deliveries = deliveries;
        _freights = freights;
        _addresses = addresses;
    }

    public async Task<Result<IReadOnlyList<DriverDeliverySummary>>> ListAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<IReadOnlyList<DriverDeliverySummary>>(DriverErrors.ProfileNotFound);
        }

        var deliveries = await _deliveries.ListByDriverAsync(profile.Id, cancellationToken);
        var freights = (await _freights.ListByIdsAsync(
            deliveries.Select(d => d.FreightRequestId).Distinct().ToList(), cancellationToken))
            .ToDictionary(f => f.Id);

        var rows = deliveries.Select(d =>
        {
            freights.TryGetValue(d.FreightRequestId, out var freight);
            var stops = freight?.Stops ?? Array.Empty<FreightStop>();
            return new DriverDeliverySummary(
                d.Id, d.FreightRequestId, d.Status, d.AgreedPrice, freight?.ScheduledPickupAt,
                stops.Count, stops.Count(s => s.IsCompleted), d.CreatedAt);
        }).ToList();

        return Result.Success<IReadOnlyList<DriverDeliverySummary>>(rows);
    }

    public async Task<Result<DeliveryRouteResponse>> GetRouteAsync(
        Guid userId, Guid deliveryId, RouteView view, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<DeliveryRouteResponse>(DriverErrors.ProfileNotFound);
        }

        var delivery = await _deliveries.GetByIdAsync(deliveryId, cancellationToken);
        if (delivery is null || delivery.DriverProfileId != profile.Id)
        {
            return Result.Failure<DeliveryRouteResponse>(MarketplaceErrors.DeliveryNotFound);
        }

        var freight = await _freights.GetByIdAsync(delivery.FreightRequestId, cancellationToken);
        if (freight is null)
        {
            return Result.Failure<DeliveryRouteResponse>(MarketplaceErrors.FreightNotFound);
        }

        var addresses = (await _addresses.ListByIdsAsync(FreightMapper.AddressIds(new[] { freight }), cancellationToken))
            .ToDictionary(a => a.Id);

        var ordered = freight.Stops.OrderBy(s => s.Sequence).ToList();
        var shown = view == RouteView.Next
            ? ordered.Where(s => !s.IsCompleted).Take(1).ToList()
            : ordered;

        var stops = shown
            .Select(s => new RouteStopResponse(
                s.Id, s.Sequence, s.Type, AddressService.ToResponse(addresses[s.AddressId]), s.Note,
                FreightMapper.ToItems(s), s.CompletedAt))
            .ToList();

        return Result.Success(new DeliveryRouteResponse(
            delivery.Id, freight.Id, freight.CargoDescription, freight.CargoWeightKg, view,
            ordered.Count, ordered.Count(s => s.IsCompleted), stops));
    }
}
