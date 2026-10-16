using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class DeliveryLocationRepository : IDeliveryLocationRepository
{
    private readonly AppDbContext _db;

    public DeliveryLocationRepository(AppDbContext db) => _db = db;

    public Task<DeliveryLocation?> GetByDeliveryIdAsync(Guid deliveryId, CancellationToken cancellationToken) =>
        _db.DeliveryLocations.FirstOrDefaultAsync(l => l.DeliveryId == deliveryId, cancellationToken);

    public async Task AddAsync(DeliveryLocation location, CancellationToken cancellationToken) =>
        await _db.DeliveryLocations.AddAsync(location, cancellationToken);
}
