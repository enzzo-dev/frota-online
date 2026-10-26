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

    public byte[] ImageData { get; private set; } = Array.Empty<byte>();

    public string ContentType { get; private set; } = string.Empty;

    public int SizeBytes { get; private set; }

    public static Signature Create(
        SignatureType type, Guid freightRequestId, Guid customerId, Guid driverProfileId,
        byte[] compressedImage, string contentType, int originalSizeBytes) => new()
    {
        Type = type,
        FreightRequestId = Guard.NotEmpty(freightRequestId, nameof(freightRequestId)),
        CustomerId = Guard.NotEmpty(customerId, nameof(customerId)),
        DriverProfileId = Guard.NotEmpty(driverProfileId, nameof(driverProfileId)),
        ImageData = compressedImage is { Length: > 0 } ? compressedImage : throw new ArgumentException("Imagem da assinatura vazia.", nameof(compressedImage)),
        ContentType = Guard.NotNullOrWhiteSpace(contentType, nameof(contentType)),
        SizeBytes = originalSizeBytes
    };
}
