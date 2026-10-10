using FrotaOnline.Application.Addresses.Dtos;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Marketplace.Dtos;

public sealed record CreateFreightRequest(
    Guid PickupAddressId,
    Guid DeliveryAddressId,
    string CargoDescription,
    decimal CargoWeightKg,
    VehicleType RequiredVehicleType,
    DateTime? ScheduledPickupAt);

public sealed record FreightResponse(
    Guid Id,
    FreightStatus Status,
    string CargoDescription,
    decimal CargoWeightKg,
    VehicleType RequiredVehicleType,
    DateTime? ScheduledPickupAt,
    DateTime ProposalDeadline,
    AddressResponse PickupAddress,
    AddressResponse DeliveryAddress,
    Guid? ApprovedProposalId,
    int PendingProposals,
    DateTime CreatedAt);

/// <summary>Proposal as the customer sees it, ordered by price (rank 1 is the cheapest).</summary>
public sealed record RankedProposalResponse(
    int Rank,
    Guid ProposalId,
    decimal Price,
    string? Message,
    string DriverName,
    VehicleType VehicleType,
    string VehicleModel,
    DateTime ExpiresAt,
    DateTime CreatedAt);

public sealed record ApprovedDeliveryResponse(
    Guid DeliveryId,
    Guid FreightRequestId,
    Guid ProposalId,
    Guid DriverProfileId,
    decimal AgreedPrice,
    decimal PlatformFee,
    DeliveryStatus Status,
    DateTime AgendaStartsAt,
    DateTime AgendaEndsAt);

/// <summary>Freight as a driver sees it before winning it: no street addresses yet.</summary>
public sealed record OpenFreightResponse(
    Guid Id,
    string CargoDescription,
    decimal CargoWeightKg,
    VehicleType RequiredVehicleType,
    DateTime? ScheduledPickupAt,
    DateTime ProposalDeadline,
    string PickupDistrict,
    string PickupCity,
    string PickupState,
    string DeliveryCity,
    string DeliveryState);

public sealed record SubmitProposalRequest(Guid VehicleId, decimal Price, string? Message);

public sealed record ProposalResponse(
    Guid Id,
    Guid FreightRequestId,
    Guid VehicleId,
    decimal Price,
    string? Message,
    ProposalStatus Status,
    DateTime ExpiresAt,
    DateTime CreatedAt);
