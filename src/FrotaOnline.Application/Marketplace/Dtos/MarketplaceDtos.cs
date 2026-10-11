using FrotaOnline.Application.Addresses.Dtos;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Marketplace.Dtos;

public sealed record CreateFreightItemRequest(string Description, int Quantity);

/// <summary>One stop of the route. Send the stops in the order the driver should follow.</summary>
public sealed record CreateFreightStopRequest(
    StopType Type,
    Guid AddressId,
    string? Note,
    IReadOnlyList<CreateFreightItemRequest>? Items);

/// <summary>The first stop must be a pickup and the last one a delivery (the final delivery address).</summary>
public sealed record CreateFreightRequest(
    IReadOnlyList<CreateFreightStopRequest> Stops,
    string CargoDescription,
    decimal CargoWeightKg,
    VehicleType RequiredVehicleType,
    DateTime? ScheduledPickupAt);

public sealed record FreightItemResponse(string Description, int Quantity);

public sealed record FreightStopResponse(
    Guid Id,
    int Sequence,
    StopType Type,
    AddressResponse Address,
    string? Note,
    IReadOnlyList<FreightItemResponse> Items,
    DateTime? CompletedAt);

public sealed record FreightResponse(
    Guid Id,
    FreightStatus Status,
    string CargoDescription,
    decimal CargoWeightKg,
    VehicleType RequiredVehicleType,
    DateTime? ScheduledPickupAt,
    DateTime ProposalDeadline,
    int PickupCount,
    int DeliveryCount,
    int? RouteDistanceMeters,
    int? RouteDurationSeconds,
    IReadOnlyList<FreightStopResponse> Stops,
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

/// <summary>
/// A stop as a driver sees it before winning the freight: the area and the cargo, no street.
/// ApproxLatitude/ApproxLongitude are rounded to two decimals (about 1 km) so the pin shows the area, not the door.
/// </summary>
public sealed record OpenStopResponse(
    int Sequence,
    StopType Type,
    string District,
    string City,
    string State,
    double? ApproxLatitude,
    double? ApproxLongitude,
    IReadOnlyList<FreightItemResponse> Items);

/// <summary>Freight as a driver sees it before winning it: no street addresses yet.</summary>
public sealed record OpenFreightResponse(
    Guid Id,
    string CargoDescription,
    decimal CargoWeightKg,
    VehicleType RequiredVehicleType,
    DateTime? ScheduledPickupAt,
    DateTime ProposalDeadline,
    int PickupCount,
    int DeliveryCount,
    int? RouteDistanceMeters,
    int? RouteDurationSeconds,
    IReadOnlyList<OpenStopResponse> Stops);

/// <summary>Full gives every stop of the run at once; Next gives only the stop the driver must do now.</summary>
public enum RouteView { Full = 1, Next = 2 }

public sealed record RouteStopResponse(
    Guid StopId,
    int Sequence,
    StopType Type,
    AddressResponse Address,
    string? Note,
    IReadOnlyList<FreightItemResponse> Items,
    DateTime? CompletedAt);

/// <summary>The route of a delivery the driver won, with full addresses.</summary>
public sealed record DeliveryRouteResponse(
    Guid DeliveryId,
    Guid FreightRequestId,
    string CargoDescription,
    decimal CargoWeightKg,
    RouteView View,
    int TotalStops,
    int CompletedStops,
    IReadOnlyList<RouteStopResponse> Stops);

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

/// <summary>A delivery the driver won, as a list row.</summary>
public sealed record DriverDeliverySummary(
    Guid DeliveryId,
    Guid FreightRequestId,
    DeliveryStatus Status,
    decimal AgreedPrice,
    DateTime? ScheduledPickupAt,
    int TotalStops,
    int CompletedStops,
    DateTime CreatedAt);
