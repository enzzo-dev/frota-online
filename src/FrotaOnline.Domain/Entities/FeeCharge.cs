using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Domain.Entities;

/// <summary>
/// A Pix the app issued so a driver can pay the platform fees they owe (freights paid outside the app).
/// When it is paid the amount is settled in the fee ledger automatically.
/// </summary>
public sealed class FeeCharge : BaseEntity, IAggregateRoot
{
    private FeeCharge()
    {
    }

    public Guid DriverProfileId { get; private set; }

    public decimal Amount { get; private set; }

    public FeeChargeStatus Status { get; private set; } = FeeChargeStatus.Pending;

    public string GatewayPaymentId { get; private set; } = string.Empty;

    public string PixPayload { get; private set; } = string.Empty;

    public string? PixQrCode { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    public DateTime? PaidAt { get; private set; }

    public static FeeCharge Create(
        Guid driverProfileId, decimal amount, string gatewayPaymentId, string payload, string? qrCode, DateTime expiresAt) => new()
    {
        DriverProfileId = Guard.NotEmpty(driverProfileId, nameof(driverProfileId)),
        Amount = Guard.Positive(amount, nameof(amount)),
        GatewayPaymentId = Guard.NotNullOrWhiteSpace(gatewayPaymentId, nameof(gatewayPaymentId)),
        PixPayload = Guard.NotNullOrWhiteSpace(payload, nameof(payload)),
        PixQrCode = qrCode,
        ExpiresAt = expiresAt
    };

    public bool IsUsable(DateTime utcNow) => Status == FeeChargeStatus.Pending && utcNow < ExpiresAt;

    public void MarkPaid(DateTime utcNow)
    {
        Status = FeeChargeStatus.Paid;
        PaidAt = utcNow;
    }

    public void Cancel()
    {
        if (Status == FeeChargeStatus.Pending)
        {
            Status = FeeChargeStatus.Cancelled;
        }
    }
}
