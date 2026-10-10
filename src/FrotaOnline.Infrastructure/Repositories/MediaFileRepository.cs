using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class MediaFileRepository : IMediaFileRepository
{
    private readonly AppDbContext _db;

    public MediaFileRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<MediaFile>> ListByOwnerAsync(
        Guid ownerUserId, IReadOnlyCollection<MediaKind> kinds, CancellationToken cancellationToken) =>
        await _db.MediaFiles
            .AsNoTracking()
            .Where(m => m.OwnerUserId == ownerUserId && kinds.Contains(m.Kind))
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<MediaFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.MediaFiles.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task<bool> StorageKeyExistsAsync(string storageKey, CancellationToken cancellationToken) =>
        _db.MediaFiles.AnyAsync(m => m.StorageKey == storageKey, cancellationToken);

    public async Task AddAsync(MediaFile file, CancellationToken cancellationToken) =>
        await _db.MediaFiles.AddAsync(file, cancellationToken);
}
