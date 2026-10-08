using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Domain.Entities;

public sealed class Delivery : BaseEntity, IAggregateRoot
{
    private static readonly IReadOnlyDictionary<DeliveryStatus, DeliveryStatus[]> AllowedTransitions =
        new Dictionary<DeliveryStatus, DeliveryStatus[]>
        {
            [DeliveryStatus.Scheduled] = new[] { DeliveryStatus.ToPickup, DeliveryStatus.Cancelled },
            [DeliveryStatus.ToPickup] = new[] { DeliveryStatus.PickedUp, DeliveryStatus.Cancelled, DeliveryStatus.Incident },
            [DeliveryStatus.PickedUp] = new[] { DeliveryStatus.InTransit, DeliveryStatus.Incident },
            [DeliveryStatus.InTransit] = new[] { DeliveryStatus.Delivered, DeliveryStatus.Incident },
            [DeliveryStatus.Incident] = new[] { DeliveryStatus.Cancelled },
            [DeliveryStatus.Delivered] = Array.Empty<DeliveryStatus>(),
            [DeliveryStatus.Cancelled] = Array.Empty<DeliveryStatus>()
        };

    private Delivery()
    {
    }

    public Guid FreightRequestId { get; private set; }

    public Guid ProposalId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid DriverProfileId { get; private set; }

    public Guid VehicleId { get; private set; }

    public decimal AgreedPrice { get; private set; }

    public decimal PlatformFee { get; private set; }

    public DeliveryStatus Status { get; private set; } = DeliveryStatus.Scheduled;

    public DateTime? PickedUpAt { get; private set; }

    public DateTime? DeliveredAt { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    public static Delivery Create(
        Guid freightRequestId, Guid proposalId, Guid customerId, Guid driverProfileId, Guid vehicleId, decimal agreedPrice) => new()
    {
        FreightRequestId = Guard.NotEmpty(freightRequestId, nameof(freightRequestId)),
        ProposalId = Guard.NotEmpty(proposalId, nameof(proposalId)),
        CustomerId = Guard.NotEmpty(customerId, nameof(customerId)),
        DriverProfileId = Guard.NotEmpty(driverProfileId, nameof(driverProfileId)),
        VehicleId = Guard.NotEmpty(vehicleId, nameof(vehicleId)),
        AgreedPrice = Guard.Positive(agreedPrice, nameof(agreedPrice)),
        PlatformFee = PlatformFees.PerFreight
    };

    public bool CanTransitionTo(DeliveryStatus next) => AllowedTransitions[Status].Contains(next);

    public void Advance(DeliveryStatus next, DateTime utcNow)
    {
        if (!CanTransitionTo(next))
        {
            throw new DomainException($"A delivery cannot go from {Status} to {next}.");
        }

        Status = next;

        switch (next)
        {
            case DeliveryStatus.PickedUp:
                PickedUpAt = utcNow;
                break;
            case DeliveryStatus.Delivered:
                DeliveredAt = utcNow;
                break;
            case DeliveryStatus.Cancelled:
                CancelledAt = utcNow;
                break;
        }
    }
}
