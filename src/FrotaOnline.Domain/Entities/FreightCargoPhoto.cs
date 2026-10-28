using FrotaOnline.Domain.Common;

namespace FrotaOnline.Domain.Entities;

public sealed class FreightCargoPhoto : BaseEntity
{
    private FreightCargoPhoto()
    {
    }

    public Guid FreightRequestId { get; private set; }

    public byte[] ImageData { get; private set; } = Array.Empty<byte>();

    public string ContentType { get; private set; } = string.Empty;

    public int SizeBytes { get; private set; }

    public static FreightCargoPhoto Create(Guid freightRequestId, byte[] compressedImage, string contentType, int originalSizeBytes) => new()
    {
        FreightRequestId = Guard.NotEmpty(freightRequestId, nameof(freightRequestId)),
        ImageData = compressedImage is { Length: > 0 } ? compressedImage : throw new ArgumentException("Foto da carga vazia.", nameof(compressedImage)),
        ContentType = Guard.NotNullOrWhiteSpace(contentType, nameof(contentType)),
        SizeBytes = originalSizeBytes
    };
}
