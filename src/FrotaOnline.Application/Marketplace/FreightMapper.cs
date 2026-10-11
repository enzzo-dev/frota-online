using FrotaOnline.Application.Addresses;
using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Marketplace;

internal static class FreightMapper
{
    public static FreightResponse ToResponse(
        FreightRequest freight, IReadOnlyDictionary<Guid, Address> addresses, int pendingProposals) => new(
        freight.Id,
        freight.Status,
        freight.CargoDescription,
        freight.CargoWeightKg,
        freight.RequiredVehicleType,
        freight.ScheduledPickupAt,
        freight.ProposalDeadline,
        freight.PickupCount,
        freight.DeliveryCount,
        freight.RouteDistanceMeters,
        freight.RouteDurationSeconds,
        freight.Stops
            .OrderBy(s => s.Sequence)
            .Select(s => new FreightStopResponse(
                s.Id, s.Sequence, s.Type, AddressService.ToResponse(addresses[s.AddressId]), s.Note, ToItems(s), s.CompletedAt))
            .ToList(),
        freight.ApprovedProposalId,
        pendingProposals,
        freight.CreatedAt);

    public static IReadOnlyList<FreightItemResponse> ToItems(FreightStop stop) =>
        stop.Items.Select(i => new FreightItemResponse(i.Description, i.Quantity)).ToList();

    public static ProposalResponse ToResponse(Proposal p) =>
        new(p.Id, p.FreightRequestId, p.VehicleId, p.Price, p.Message, p.Status, p.ExpiresAt, p.CreatedAt);
}
