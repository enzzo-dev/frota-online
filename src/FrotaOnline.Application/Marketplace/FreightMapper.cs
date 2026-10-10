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
        AddressService.ToResponse(addresses[freight.PickupAddressId]),
        AddressService.ToResponse(addresses[freight.DeliveryAddressId]),
        freight.ApprovedProposalId,
        pendingProposals,
        freight.CreatedAt);

    public static ProposalResponse ToResponse(Proposal p) =>
        new(p.Id, p.FreightRequestId, p.VehicleId, p.Price, p.Message, p.Status, p.ExpiresAt, p.CreatedAt);
}
