using FrotaOnline.Application.Abstractions.Addresses;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class AddressRepository : IAddressRepository
{
    private readonly AppDbContext _db;

    public AddressRepository(AppDbContext db) => _db = db;

    public Task<Address?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Addresses.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Address>> ListByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken) =>
        await _db.Addresses.AsNoTracking().Where(a => a.OwnerUserId == ownerUserId && a.DeletedAt == null).OrderBy(a => a.CreatedAt).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Address>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await _db.Addresses.AsNoTracking().Where(a => ids.Contains(a.Id)).ToListAsync(cancellationToken);

    public async Task AddAsync(Address address, CancellationToken cancellationToken) =>
        await _db.Addresses.AddAsync(address, cancellationToken);

    public void Remove(Address address) => _db.Addresses.Remove(address);
}
