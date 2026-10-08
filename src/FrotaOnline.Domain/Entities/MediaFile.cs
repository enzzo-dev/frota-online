using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Domain.Entities;

public sealed class MediaFile : BaseEntity
{
    private MediaFile()
    {
    }

    public Guid OwnerUserId { get; private set; }

    public MediaKind Kind { get; private set; }

    /// <summary>Object key inside the storage bucket.</summary>
    public string StorageKey { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public static MediaFile Create(Guid ownerUserId, MediaKind kind, string storageKey, string contentType, long sizeBytes) => new()
    {
        OwnerUserId = Guard.NotEmpty(ownerUserId, nameof(ownerUserId)),
        Kind = kind,
        StorageKey = Guard.NotNullOrWhiteSpace(storageKey, nameof(storageKey)),
        ContentType = Guard.NotNullOrWhiteSpace(contentType, nameof(contentType)),
        SizeBytes = sizeBytes > 0 ? sizeBytes : throw new DomainException("sizeBytes must be greater than zero.")
    };
}
