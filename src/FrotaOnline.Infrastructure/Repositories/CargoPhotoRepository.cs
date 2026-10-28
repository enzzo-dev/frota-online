using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class CargoPhotoRepository : ICargoPhotoRepository
{
    private readonly AppDbContext _db;

    public CargoPhotoRepository(AppDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid freightRequestId, CancellationToken cancellationToken) =>
        _db.FreightCargoPhotos.AnyAsync(p => p.FreightRequestId == freightRequestId, cancellationToken);

    public Task<FreightCargoPhoto?> GetAsync(Guid freightRequestId, CancellationToken cancellationToken) =>
        _db.FreightCargoPhotos.AsNoTracking().FirstOrDefaultAsync(p => p.FreightRequestId == freightRequestId, cancellationToken);

    public async Task AddAsync(FreightCargoPhoto photo, CancellationToken cancellationToken) =>
        await _db.FreightCargoPhotos.AddAsync(photo, cancellationToken);
}
