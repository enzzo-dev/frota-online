using FrotaOnline.Domain.Common;

namespace FrotaOnline.Domain.Entities;

/// <summary>The last known position of the driver during a delivery. One row per delivery, overwritten on every update.</summary>
public sealed class DeliveryLocation : BaseEntity
{
    private DeliveryLocation()
    {
    }

    public Guid DeliveryId { get; private set; }

    public double Latitude { get; private set; }

    public double Longitude { get; private set; }

    /// <summary>Direction of travel in degrees (0 to 360), when the device reports it.</summary>
    public double? Heading { get; private set; }

    public double? SpeedKmh { get; private set; }

    public DateTime RecordedAt { get; private set; }

    public static DeliveryLocation Create(
        Guid deliveryId, double latitude, double longitude, double? heading, double? speedKmh, DateTime recordedAt) => new()
    {
        DeliveryId = Guard.NotEmpty(deliveryId, nameof(deliveryId)),
        Latitude = latitude,
        Longitude = longitude,
        Heading = heading,
        SpeedKmh = speedKmh,
        RecordedAt = recordedAt
    };

    public void MoveTo(double latitude, double longitude, double? heading, double? speedKmh, DateTime recordedAt)
    {
        Latitude = latitude;
        Longitude = longitude;
        Heading = heading;
        SpeedKmh = speedKmh;
        RecordedAt = recordedAt;
        Touch(recordedAt);
    }
}
