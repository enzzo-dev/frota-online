using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Domain.Entities;

/// <summary>Records how the customer paid the driver. The platform never holds freight money.</summary>
public sealed class Payment : BaseEntity, IAggregateRoot
{
    private Payment()
    {
    }

    public Guid DeliveryId { get; private set; }

    public decimal Amount { get; private set; }

    public PaymentMethod Method { get; private set; }

    public PaymentStatus Status { get; private set; } = PaymentStatus.Pending;

    /// <summary>True when settled outside the app (cash, direct Pix); the platform fee then goes to the fee ledger.</summary>
    public bool PaidOutsideApp { get; private set; }

    public DateTime? PaidAt { get; private set; }

    public string? ExternalReference { get; private set; }

    /// <summary>Id of the charge at the payment provider; set when the app itself issued the Pix.</summary>
    public string? GatewayPaymentId { get; private set; }

    /// <summary>Which provider holds <see cref="GatewayPaymentId"/>: <see cref="PaymentProviders.Asaas"/> or <see cref="PaymentProviders.MercadoPago"/>.</summary>
    public string? Gateway { get; private set; }

    /// <summary>Pix "copia e cola" code the customer pays.</summary>
    public string? PixPayload { get; private set; }

    /// <summary>QR code image, base64 (PNG).</summary>
    public string? PixQrCode { get; private set; }

    public DateTime? PixExpiresAt { get; private set; }

    public static Payment Create(Guid deliveryId, decimal amount, PaymentMethod method, bool paidOutsideApp) => new()
    {
        DeliveryId = Guard.NotEmpty(deliveryId, nameof(deliveryId)),
        Amount = Guard.Positive(amount, nameof(amount)),
        Method = method,
        PaidOutsideApp = paidOutsideApp
    };

    /// <summary>
    /// A Pix issued by the app. The provider splits it on receipt: the driver gets the price minus the platform fee,
    /// the platform keeps the fee. <see cref="PaidOutsideApp"/> is false, so no fee is left to collect later.
    /// </summary>
    public static Payment CreatePix(
        Guid deliveryId, decimal amount, string gatewayPaymentId, string payload, string? qrCode, DateTime expiresAt) => new()
    {
        DeliveryId = Guard.NotEmpty(deliveryId, nameof(deliveryId)),
        Amount = Guard.Positive(amount, nameof(amount)),
        Method = PaymentMethod.Pix,
        PaidOutsideApp = false,
        Gateway = PaymentProviders.Asaas,
        GatewayPaymentId = Guard.NotNullOrWhiteSpace(gatewayPaymentId, nameof(gatewayPaymentId)),
        PixPayload = Guard.NotNullOrWhiteSpace(payload, nameof(payload)),
        PixQrCode = qrCode,
        PixExpiresAt = expiresAt
    };

    /// <summary>
    /// A card or wallet (Apple Pay, Google Pay) payment started in the app. It stays pending until the provider confirms it
    /// by webhook, so the money is only counted once.
    /// </summary>
    public static Payment CreateOnline(Guid deliveryId, decimal amount, string gateway, string gatewayPaymentId) => new()
    {
        DeliveryId = Guard.NotEmpty(deliveryId, nameof(deliveryId)),
        Amount = Guard.Positive(amount, nameof(amount)),
        Method = PaymentMethod.Card,
        PaidOutsideApp = false,
        Gateway = Guard.NotNullOrWhiteSpace(gateway, nameof(gateway)),
        GatewayPaymentId = Guard.NotNullOrWhiteSpace(gatewayPaymentId, nameof(gatewayPaymentId))
    };

    /// <summary>The earlier attempt expired or failed: attach a fresh Pix to the same payment (one payment per delivery).</summary>
    public void ReissuePix(string gatewayPaymentId, string payload, string? qrCode, DateTime expiresAt)
    {
        EnsureCanRestart();
        Method = PaymentMethod.Pix;
        Status = PaymentStatus.Pending;
        Gateway = PaymentProviders.Asaas;
        GatewayPaymentId = Guard.NotNullOrWhiteSpace(gatewayPaymentId, nameof(gatewayPaymentId));
        PixPayload = Guard.NotNullOrWhiteSpace(payload, nameof(payload));
        PixQrCode = qrCode;
        PixExpiresAt = expiresAt;
    }

    /// <summary>The earlier attempt was cancelled or failed: start a new card or wallet charge on the same payment.</summary>
    public void RestartOnline(string gateway, string gatewayPaymentId)
    {
        EnsureCanRestart();
        Method = PaymentMethod.Card;
        Status = PaymentStatus.Pending;
        Gateway = Guard.NotNullOrWhiteSpace(gateway, nameof(gateway));
        GatewayPaymentId = Guard.NotNullOrWhiteSpace(gatewayPaymentId, nameof(gatewayPaymentId));
        PixPayload = null;
        PixQrCode = null;
        PixExpiresAt = null;
    }

    private void EnsureCanRestart()
    {
        if (Status == PaymentStatus.Paid)
        {
            throw new DomainException("A paid payment cannot be charged again.");
        }
    }

    public bool IsPixUsable(DateTime utcNow) =>
        Status == PaymentStatus.Pending && GatewayPaymentId is not null && PixExpiresAt is { } end && utcNow < end;

    public void MarkPaid(DateTime utcNow, string? externalReference = null)
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new DomainException($"A payment in status {Status} cannot be marked as paid.");
        }

        Status = PaymentStatus.Paid;
        PaidAt = utcNow;
        ExternalReference = externalReference;
    }

    public void MarkFailed()
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new DomainException($"A payment in status {Status} cannot be marked as failed.");
        }

        Status = PaymentStatus.Failed;
    }
}

/// <summary>The providers a charge can live at.</summary>
public static class PaymentProviders
{
    /// <summary>Pix, card and subscriptions.</summary>
    public const string Asaas = "Asaas";

    /// <summary>Wallet and card payments with a token made by the app (Apple Pay); the driver is paid through their own account.</summary>
    public const string MercadoPago = "MercadoPago";
}
