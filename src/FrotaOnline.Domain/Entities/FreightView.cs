using FrotaOnline.Domain.Common;

namespace FrotaOnline.Domain.Entities;

public sealed class FreightView : BaseEntity
{
    private FreightView()
    {
    }

    public Guid FreightRequestId { get; private set; }

    public Guid DriverProfileId { get; private set; }

    public DateTime FirstViewedAt { get; private set; }

    public DateTime LastViewedAt { get; private set; }

    public int ViewCount { get; private set; }

    public static FreightView Create(Guid freightRequestId, Guid driverProfileId, DateTime utcNow) => new()
    {
        FreightRequestId = Guard.NotEmpty(freightRequestId, nameof(freightRequestId)),
        DriverProfileId = Guard.NotEmpty(driverProfileId, nameof(driverProfileId)),
        FirstViewedAt = utcNow,
        LastViewedAt = utcNow,
        ViewCount = 1
    };

    public void Touch(DateTime utcNow)
    {
        LastViewedAt = utcNow;
        ViewCount++;
    }
}
