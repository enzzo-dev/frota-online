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
    DateTime CreatedAt,
    string? DriverPhone);

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
    IReadOnlyList<DeliveryEventResponse> Timeline,
    string? DriverPhone);

/// <summary>
/// The few fields a screen polls to know whether anything changed. <see cref="Version"/> changes whenever the status, the
/// completed stops, the payment or the timeline change, so the client fetches the full detail only when it differs.
/// </summary>
public sealed record DeliveryStatusResponse(
    Guid DeliveryId,
    DeliveryStatus Status,
    int TotalStops,
    int CompletedStops,
    PaymentStatus? Payment,
    DateTime? LastEventAt,
    DriverLocationResponse? DriverLocation,
    bool CustomerSigned,
    bool DriverSigned,
    string Version)
{
    public static DeliveryStatusResponse Create(
        Guid deliveryId, DeliveryStatus status, int totalStops, int completedStops, PaymentStatus? payment, DateTime? lastEventAt,
        DriverLocationResponse? driverLocation = null, bool customerSigned = false, bool driverSigned = false) =>
        new(deliveryId, status, totalStops, completedStops, payment, lastEventAt, driverLocation, customerSigned, driverSigned,
            $"{(int)status}.{completedStops}.{(payment is null ? -1 : (int)payment)}.{lastEventAt?.Ticks ?? 0}.{(customerSigned ? 1 : 0)}{(driverSigned ? 1 : 0)}");
}

public sealed record DriverLocationResponse(double Latitude, double Longitude, double? Heading, double? SpeedKmh, DateTime RecordedAt);

public sealed record UpdateLocationRequest(double Latitude, double Longitude, double? Heading, double? SpeedKmh);

/// <summary>An admin records that the driver paid part or all of the fees owed to the platform.</summary>
public sealed record SettleFeeRequest(decimal Amount, string Description);

public sealed record FeeEntryResponse(
    Guid Id, Guid? DeliveryId, FeeEntryType Type, decimal Amount, string Description, DateTime CreatedAt);

/// <summary>Balance = accrued - settled. Entries are the 200 most recent, newest first.</summary>
public sealed record FeeStatementResponse(
    decimal Balance, decimal TotalAccrued, decimal TotalSettled, IReadOnlyList<FeeEntryResponse> Entries);
