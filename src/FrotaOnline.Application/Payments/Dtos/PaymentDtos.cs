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

/// <summary>Card typed by the customer. Sent once to the provider; never stored.</summary>
public sealed record CardInput(string HolderName, string Number, string ExpiryMonth, string ExpiryYear, string Ccv);

public sealed record CardHolderInput(string TaxId, string PostalCode, string AddressNumber, string? Phone);

/// <summary>Pay with a saved card (<c>SavedCardId</c>) or with a new one (<c>Card</c> + <c>Holder</c>), never both.</summary>
public sealed record PayWithCardRequest(Guid? SavedCardId, CardInput? Card, CardHolderInput? Holder, bool SaveCard);

public sealed record CardPaymentResponse(
    Guid PaymentId,
    Guid DeliveryId,
    decimal Amount,
    decimal DriverReceives,
    decimal PlatformFee,
    PaymentStatus Status,
    string? Brand,
    string? Last4,
    bool CardSaved);

public sealed record SavedCardResponse(Guid Id, string Brand, string Last4);

/// <summary>
/// Payment with a token the app made (Apple Pay through the Mercado Pago SDK, or a card). <c>PaymentMethodId</c> is the card
/// network the token belongs to, such as <c>visa</c> or <c>master</c>.
/// </summary>
public sealed record WalletPaymentRequest(string CardToken, string PaymentMethodId);

public sealed record WalletPaymentResponse(
    Guid PaymentId,
    Guid DeliveryId,
    decimal Amount,
    decimal DriverReceives,
    decimal PlatformFee,
    PaymentStatus Status,
    string ProviderStatus);

public sealed record WalletAccountResponse(bool Connected);

public sealed record WalletAuthorizationResponse(string AuthorizationUrl);

public sealed record FeeChargeResponse(
    Guid Id, decimal Amount, FeeChargeStatus Status, string PixPayload, string? PixQrCode, DateTime ExpiresAt);

/// <summary>What the webhook endpoint needs from a provider notification.</summary>
public sealed record WebhookNotification(string EventId, string Event, WebhookPayment? Payment, string? SubscriptionId);

public sealed record WebhookPayment(string Id, string? SubscriptionId, string? ExternalReference, decimal Value);
