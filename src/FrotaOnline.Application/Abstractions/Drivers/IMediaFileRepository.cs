using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Abstractions.Drivers;

public interface IMediaFileRepository
{
    Task<IReadOnlyList<MediaFile>> ListByOwnerAsync(
        Guid ownerUserId, IReadOnlyCollection<MediaKind> kinds, CancellationToken cancellationToken);

    Task<bool> StorageKeyExistsAsync(string storageKey, CancellationToken cancellationToken);

    Task AddAsync(MediaFile file, CancellationToken cancellationToken);
}
