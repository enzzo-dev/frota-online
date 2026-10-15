using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Domain.Entities;

/// <summary>Append-only ledger of platform fees owed by a driver. Balance = accrued - settled.</summary>
public sealed class FeeLedger : BaseEntity
{
    private FeeLedger()
    {
    }

    public Guid DriverProfileId { get; private set; }

    public Guid? DeliveryId { get; private set; }

    public FeeEntryType EntryType { get; private set; }

    public decimal Amount { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public static FeeLedger Accrue(Guid driverProfileId, Guid deliveryId, decimal amount) => new()
    {
        DriverProfileId = Guard.NotEmpty(driverProfileId, nameof(driverProfileId)),
        DeliveryId = Guard.NotEmpty(deliveryId, nameof(deliveryId)),
        EntryType = FeeEntryType.FeeAccrued,
        Amount = Guard.Positive(amount, nameof(amount)),
        Description = "Taxa da plataforma gerada (pagamento feito fora do aplicativo)"
    };

    public static FeeLedger Settle(Guid driverProfileId, Guid? deliveryId, decimal amount, string description) => new()
    {
        DriverProfileId = Guard.NotEmpty(driverProfileId, nameof(driverProfileId)),
        DeliveryId = deliveryId,
        EntryType = FeeEntryType.FeeSettled,
        Amount = Guard.Positive(amount, nameof(amount)),
        Description = Guard.NotNullOrWhiteSpace(description, nameof(description))
    };
}
