using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Domain.Entities;

/// <summary>Immutable audit trail entry for a delivery status change.</summary>
public sealed class DeliveryEvent : BaseEntity
{
    private DeliveryEvent()
    {
    }

    public Guid DeliveryId { get; private set; }

    public DeliveryStatus Status { get; private set; }

    public DateTime OccurredAt { get; private set; }

    public string? Note { get; private set; }

    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    public Guid? MediaFileId { get; private set; }

    public static DeliveryEvent Create(
        Guid deliveryId, DeliveryStatus status, DateTime occurredAt, string? note,
        double? latitude, double? longitude, Guid? mediaFileId) => new()
    {
        DeliveryId = Guard.NotEmpty(deliveryId, nameof(deliveryId)),
        Status = status,
        OccurredAt = occurredAt,
        Note = note?.Trim(),
        Latitude = latitude,
        Longitude = longitude,
        MediaFileId = mediaFileId
    };
}
