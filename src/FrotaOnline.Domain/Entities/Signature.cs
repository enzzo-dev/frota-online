using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Domain.Entities;

public sealed class Signature : BaseEntity
{
    private Signature()
    {
    }

    public SignatureType Type { get; private set; }

    public Guid FreightRequestId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid DriverProfileId { get; private set; }

    public string StoragePath { get; private set; } = string.Empty;

    public static Signature Create(
        SignatureType type, Guid freightRequestId, Guid customerId, Guid driverProfileId, string storagePath) => new()
    {
        Type = type,
        FreightRequestId = Guard.NotEmpty(freightRequestId, nameof(freightRequestId)),
        CustomerId = Guard.NotEmpty(customerId, nameof(customerId)),
        DriverProfileId = Guard.NotEmpty(driverProfileId, nameof(driverProfileId)),
        StoragePath = Guard.NotNullOrWhiteSpace(storagePath, nameof(storagePath))
    };
}
