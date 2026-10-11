using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Payments.Dtos;

public sealed record SubscribeRequest(SubscriptionPlan Plan);

public sealed record PlanOption(SubscriptionPlan Plan, decimal MonthlyPrice, int? MaxVehicles);

public sealed record SubscriptionResponse(
    Guid Id,
    SubscriptionPlan Plan,
    SubscriptionStatus Status,
    decimal MonthlyPrice,
    int? MaxVehicles,
    bool HasAccess,
    DateTime? StartsAt,
    DateTime? EndsAt,
    string? PaymentUrl);

/// <summary>The plans on offer and, if the driver has one, the current subscription.</summary>
public sealed record SubscriptionOverview(IReadOnlyList<PlanOption> Plans, SubscriptionResponse? Current);

public sealed record PayoutAccountResponse(bool Configured, string? WalletId);

public sealed record UpdatePayoutAccountRequest(string WalletId);

public sealed record CreatePixRequest(string TaxId);

public sealed record PixPaymentResponse(
    Guid PaymentId,
    Guid DeliveryId,
    decimal Amount,
    decimal DriverReceives,
    decimal PlatformFee,
    PaymentStatus Status,
    string PixPayload,
    string? PixQrCode,
    DateTime ExpiresAt);

public sealed record FeeChargeResponse(
    Guid Id, decimal Amount, FeeChargeStatus Status, string PixPayload, string? PixQrCode, DateTime ExpiresAt);

/// <summary>What the webhook endpoint needs from a provider notification.</summary>
public sealed record WebhookNotification(string EventId, string Event, WebhookPayment? Payment, string? SubscriptionId);

public sealed record WebhookPayment(string Id, string? SubscriptionId, string? ExternalReference, decimal Value);
