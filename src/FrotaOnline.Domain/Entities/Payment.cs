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
        GatewayPaymentId = Guard.NotNullOrWhiteSpace(gatewayPaymentId, nameof(gatewayPaymentId)),
        PixPayload = Guard.NotNullOrWhiteSpace(payload, nameof(payload)),
        PixQrCode = qrCode,
        PixExpiresAt = expiresAt
    };

    /// <summary>The previous Pix expired unpaid: attach a fresh one to the same payment (there is one payment per delivery).</summary>
    public void ReissuePix(string gatewayPaymentId, string payload, string? qrCode, DateTime expiresAt)
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new DomainException($"A payment in status {Status} cannot get a new Pix.");
        }

        GatewayPaymentId = Guard.NotNullOrWhiteSpace(gatewayPaymentId, nameof(gatewayPaymentId));
        PixPayload = Guard.NotNullOrWhiteSpace(payload, nameof(payload));
        PixQrCode = qrCode;
        PixExpiresAt = expiresAt;
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
