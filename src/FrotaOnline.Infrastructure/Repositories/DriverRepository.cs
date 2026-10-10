using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class DriverRepository : IDriverRepository
{
    private readonly AppDbContext _db;

    public DriverRepository(AppDbContext db) => _db = db;

    public Task<DriverProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.DriverProfiles.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<DriverProfile>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await _db.DriverProfiles.Where(d => ids.Contains(d.Id)).ToListAsync(cancellationToken);

    public Task<DriverProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        _db.DriverProfiles.FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<DriverProfile>> ListByStatusAsync(DriverStatus status, CancellationToken cancellationToken) =>
        await _db.DriverProfiles
            .Where(d => d.Status == status)
            .OrderBy(d => d.UpdatedAt ?? d.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<bool> TaxIdInUseAsync(string taxId, Guid exceptUserId, CancellationToken cancellationToken) =>
        _db.DriverProfiles.AnyAsync(d => d.TaxId == taxId && d.UserId != exceptUserId, cancellationToken);

    public Task<bool> CnhInUseAsync(string cnhNumber, Guid exceptUserId, CancellationToken cancellationToken) =>
        _db.DriverProfiles.AnyAsync(d => d.CnhNumber == cnhNumber && d.UserId != exceptUserId, cancellationToken);

    public async Task AddAsync(DriverProfile profile, CancellationToken cancellationToken) =>
        await _db.DriverProfiles.AddAsync(profile, cancellationToken);
}
