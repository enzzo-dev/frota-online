using FrotaOnline.Domain.Common;

namespace FrotaOnline.Domain.Entities;

public sealed class DriverAgendaSlot : BaseEntity
{
    private DriverAgendaSlot()
    {
    }

    public Guid DriverProfileId { get; private set; }

    public Guid DeliveryId { get; private set; }

    public DateTime StartsAt { get; private set; }

    public DateTime EndsAt { get; private set; }

    public DateTime? ReleasedAt { get; private set; }

    public bool IsActive => ReleasedAt is null;

    public static DriverAgendaSlot Create(Guid driverProfileId, Guid deliveryId, DateTime startsAt, DateTime endsAt)
    {
        if (endsAt <= startsAt)
        {
            throw new DomainException("The slot must end after it starts.");
        }

        return new DriverAgendaSlot
        {
            DriverProfileId = Guard.NotEmpty(driverProfileId, nameof(driverProfileId)),
            DeliveryId = Guard.NotEmpty(deliveryId, nameof(deliveryId)),
            StartsAt = startsAt,
            EndsAt = endsAt
        };
    }

    public bool Overlaps(DateTime startsAt, DateTime endsAt) => IsActive && startsAt < EndsAt && endsAt > StartsAt;

    public void Release(DateTime utcNow) => ReleasedAt ??= utcNow;
}
