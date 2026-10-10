using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Addresses;
using FrotaOnline.Application.Admin.Dtos;
using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Tests.Auth;
using FrotaOnline.Tests.Onboarding;

namespace FrotaOnline.Tests.Marketplace;

// In-memory stand-ins for the SQL projections in Infrastructure/Queries. They follow the same
// contract (ownership, filters, ordering, counters) so the services can be tested without a database.
// The real SQL is covered by integration tests against PostgreSQL.

internal sealed class FakeFreightQueries : IFreightQueries
{
    private readonly FakeFreightRepository _freights;
    private readonly FakeProposalRepository _proposals;
    private readonly FakeAddressRepository _addresses;
    private readonly FakeDriverRepository _drivers;
    private readonly FakeUserRepository _users;
    private readonly FakeVehicleRepository _vehicles;

    public FakeFreightQueries(
        FakeFreightRepository freights,
        FakeProposalRepository proposals,
        FakeAddressRepository addresses,
        FakeDriverRepository drivers,
        FakeUserRepository users,
        FakeVehicleRepository vehicles)
    {
        _freights = freights;
        _proposals = proposals;
        _addresses = addresses;
        _drivers = drivers;
        _users = users;
        _vehicles = vehicles;
    }

    public Task<IReadOnlyList<OpenFreightResponse>> ListOpenAsync(
        DateTime utcNow, IReadOnlyCollection<VehicleType> vehicleTypes, CancellationToken cancellationToken)
    {
        var rows = _freights.Items
            .Where(f => f.Status == FreightStatus.Open && f.ProposalDeadline > utcNow && vehicleTypes.Contains(f.RequiredVehicleType))
            .OrderByDescending(f => f.CreatedAt)
            .Take(100)
            .Select(f => new OpenFreightResponse(
                f.Id, f.CargoDescription, f.CargoWeightKg, f.RequiredVehicleType, f.ScheduledPickupAt, f.ProposalDeadline,
                f.PickupCount, f.DeliveryCount,
                f.Stops.OrderBy(s => s.Sequence)
                    .Select(s =>
                    {
                        var address = FindAddress(s.AddressId);
                        return new OpenStopResponse(s.Sequence, s.Type, address.District, address.City, address.State, StopItemsFake.Of(s));
                    })
                    .ToList()))
            .ToList();

        return Task.FromResult<IReadOnlyList<OpenFreightResponse>>(rows);
    }

    public Task<IReadOnlyList<FreightResponse>> ListByCustomerAsync(
        Guid customerId, FreightStatus? status, CancellationToken cancellationToken)
    {
        var rows = _freights.Items
            .Where(f => f.CustomerId == customerId && (status is null || f.Status == status))
            .OrderByDescending(f => f.CreatedAt)
            .Select(ToResponse)
            .ToList();

        return Task.FromResult<IReadOnlyList<FreightResponse>>(rows);
    }

    public Task<FreightResponse?> GetForCustomerAsync(Guid customerId, Guid freightId, CancellationToken cancellationToken)
    {
        var freight = _freights.Items.FirstOrDefault(f => f.Id == freightId && f.CustomerId == customerId);
        return Task.FromResult(freight is null ? null : ToResponse(freight));
    }

    public Task<IReadOnlyList<RankedProposalResponse>?> ListRankedProposalsAsync(
        Guid customerId, Guid freightId, DateTime utcNow, CancellationToken cancellationToken)
    {
        if (!_freights.Items.Any(f => f.Id == freightId && f.CustomerId == customerId))
        {
            return Task.FromResult<IReadOnlyList<RankedProposalResponse>?>(null);
        }

        var ranked = _proposals.Items
            .Where(p => p.FreightRequestId == freightId && p.Status == ProposalStatus.Pending && p.ExpiresAt > utcNow)
            .OrderBy(p => p.Price)
            .ThenBy(p => p.CreatedAt)
            .Select(p =>
            {
                var driver = _drivers.Items.First(d => d.Id == p.DriverProfileId);
                var user = _users.Items.First(u => u.Id == driver.UserId);
                var vehicle = _vehicles.Items.First(v => v.Id == p.VehicleId);
                return (Proposal: p, user.FullName, Vehicle: vehicle);
            })
            .Select((row, index) => new RankedProposalResponse(
                index + 1, row.Proposal.Id, row.Proposal.Price, row.Proposal.Message, row.FullName,
                row.Vehicle.Type, row.Vehicle.Model, row.Proposal.ExpiresAt, row.Proposal.CreatedAt))
            .ToList();

        return Task.FromResult<IReadOnlyList<RankedProposalResponse>?>(ranked);
    }

    private Address FindAddress(Guid id) => _addresses.Items.First(a => a.Id == id);

    private FreightResponse ToResponse(FreightRequest freight) => new(
        freight.Id, freight.Status, freight.CargoDescription, freight.CargoWeightKg, freight.RequiredVehicleType,
        freight.ScheduledPickupAt, freight.ProposalDeadline, freight.PickupCount, freight.DeliveryCount,
        freight.Stops.OrderBy(s => s.Sequence)
            .Select(s => new FreightStopResponse(
                s.Id, s.Sequence, s.Type, AddressService.ToResponse(FindAddress(s.AddressId)), s.Note,
                StopItemsFake.Of(s), s.CompletedAt))
            .ToList(),
        freight.ApprovedProposalId,
        _proposals.Items.Count(p => p.FreightRequestId == freight.Id && p.Status == ProposalStatus.Pending),
        freight.CreatedAt);
}

internal sealed class FakeDeliveryQueries : IDeliveryQueries
{
    private readonly FakeDeliveryRepository _deliveries;
    private readonly FakeFreightRepository _freights;
    private readonly FakeAddressRepository _addresses;

    public FakeDeliveryQueries(FakeDeliveryRepository deliveries, FakeFreightRepository freights, FakeAddressRepository addresses)
    {
        _deliveries = deliveries;
        _freights = freights;
        _addresses = addresses;
    }

    public Task<IReadOnlyList<DriverDeliverySummary>> ListByDriverAsync(Guid driverProfileId, CancellationToken cancellationToken)
    {
        var rows = _deliveries.Items
            .Where(d => d.DriverProfileId == driverProfileId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d =>
            {
                var freight = _freights.Items.First(f => f.Id == d.FreightRequestId);
                return new DriverDeliverySummary(
                    d.Id, d.FreightRequestId, d.Status, d.AgreedPrice, freight.ScheduledPickupAt,
                    freight.Stops.Count, freight.Stops.Count(s => s.IsCompleted), d.CreatedAt);
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<DriverDeliverySummary>>(rows);
    }

    public Task<DeliveryRouteResponse?> GetRouteAsync(
        Guid driverProfileId, Guid deliveryId, RouteView view, CancellationToken cancellationToken)
    {
        var delivery = _deliveries.Items.FirstOrDefault(d => d.Id == deliveryId && d.DriverProfileId == driverProfileId);
        var freight = delivery is null ? null : _freights.Items.FirstOrDefault(f => f.Id == delivery.FreightRequestId);
        if (delivery is null || freight is null)
        {
            return Task.FromResult<DeliveryRouteResponse?>(null);
        }

        var ordered = freight.Stops.OrderBy(s => s.Sequence).ToList();
        var shown = view == RouteView.Next ? ordered.Where(s => !s.IsCompleted).Take(1).ToList() : ordered;

        var stops = shown
            .Select(s => new RouteStopResponse(
                s.Id, s.Sequence, s.Type, AddressService.ToResponse(_addresses.Items.First(a => a.Id == s.AddressId)),
                s.Note, StopItemsFake.Of(s), s.CompletedAt))
            .ToList();

        return Task.FromResult<DeliveryRouteResponse?>(new DeliveryRouteResponse(
            delivery.Id, freight.Id, freight.CargoDescription, freight.CargoWeightKg, view,
            ordered.Count, ordered.Count(s => s.IsCompleted), stops));
    }
}

internal sealed class FakeDriverReviewQueries : IDriverReviewQueries
{
    private readonly FakeDriverRepository _drivers;
    private readonly FakeUserRepository _users;

    public FakeDriverReviewQueries(FakeDriverRepository drivers, FakeUserRepository users)
    {
        _drivers = drivers;
        _users = users;
    }

    public Task<IReadOnlyList<DriverReviewSummary>> ListByStatusAsync(DriverStatus status, CancellationToken cancellationToken)
    {
        var rows = _drivers.Items
            .Where(d => d.Status == status)
            .Join(_users.Items, d => d.UserId, u => u.Id, (d, u) => new DriverReviewSummary(
                d.Id, u.Id, u.FullName, u.Email, u.PhoneNumber, d.Status, d.UpdatedAt ?? d.CreatedAt))
            .OrderBy(r => r.LastUpdatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<DriverReviewSummary>>(rows);
    }
}

internal static class StopItemsFake
{
    public static IReadOnlyList<FreightItemResponse> Of(FreightStop stop) =>
        stop.Items.Select(i => new FreightItemResponse(i.Description, i.Quantity)).ToList();
}
