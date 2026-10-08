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

    public static Payment Create(Guid deliveryId, decimal amount, PaymentMethod method, bool paidOutsideApp) => new()
    {
        DeliveryId = Guard.NotEmpty(deliveryId, nameof(deliveryId)),
        Amount = Guard.Positive(amount, nameof(amount)),
        Method = method,
        PaidOutsideApp = paidOutsideApp
    };

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
