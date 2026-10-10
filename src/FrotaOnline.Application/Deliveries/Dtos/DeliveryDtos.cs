using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Deliveries.Dtos;

/// <summary>Everything is optional. Send the coordinates together. The proof is a DeliveryProof file the driver registered.</summary>
public sealed record CompleteStopRequest(string? Note, double? Latitude, double? Longitude, Guid? ProofMediaFileId);

public sealed record ReportIncidentRequest(string Note);

public sealed record CancelDeliveryRequest(string Reason);

/// <summary>Where the run stands after an action. NextStopId is null once every stop is done.</summary>
public sealed record DeliveryProgressResponse(
    Guid DeliveryId,
    DeliveryStatus Status,
    int TotalStops,
    int CompletedStops,
    Guid? NextStopId);

/// <summary>The driver confirms the money reached them. The platform never holds it.</summary>
public sealed record ConfirmPaymentRequest(PaymentMethod Method, string? Reference);

public sealed record PaymentResponse(
    Guid Id, Guid DeliveryId, decimal Amount, PaymentMethod Method, PaymentStatus Status, DateTime? PaidAt);

public sealed record DeliveryEventResponse(
    DeliveryStatus Status, DateTime OccurredAt, string? Note, double? Latitude, double? Longitude, bool HasProof);

/// <summary>A delivery as its customer sees it in a list.</summary>
public sealed record CustomerDeliverySummary(
    Guid DeliveryId,
    Guid FreightRequestId,
    DeliveryStatus Status,
    decimal AgreedPrice,
    string DriverName,
    VehicleType VehicleType,
    string VehicleModel,
    string VehiclePlate,
    int TotalStops,
    int CompletedStops,
    PaymentStatus? Payment,
    DateTime CreatedAt);

public sealed record CustomerDeliveryDetail(
    Guid DeliveryId,
    Guid FreightRequestId,
    DeliveryStatus Status,
    decimal AgreedPrice,
    string DriverName,
    VehicleType VehicleType,
    string VehicleModel,
    string VehiclePlate,
    PaymentStatus? Payment,
    DateTime? PickedUpAt,
    DateTime? DeliveredAt,
    DateTime? CancelledAt,
    IReadOnlyList<FreightStopResponse> Stops,
    IReadOnlyList<DeliveryEventResponse> Timeline);

/// <summary>An admin records that the driver paid part or all of the fees owed to the platform.</summary>
public sealed record SettleFeeRequest(decimal Amount, string Description);

public sealed record FeeEntryResponse(
    Guid Id, Guid? DeliveryId, FeeEntryType Type, decimal Amount, string Description, DateTime CreatedAt);

/// <summary>Balance = accrued - settled. Entries are the 200 most recent, newest first.</summary>
public sealed record FeeStatementResponse(
    decimal Balance, decimal TotalAccrued, decimal TotalSettled, IReadOnlyList<FeeEntryResponse> Entries);
