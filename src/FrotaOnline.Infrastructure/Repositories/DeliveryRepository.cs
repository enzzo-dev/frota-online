using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class DeliveryRepository : IDeliveryRepository
{
    private readonly AppDbContext _db;

    public DeliveryRepository(AppDbContext db) => _db = db;

    public Task<Delivery?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Deliveries.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Delivery>> ListByDriverAsync(Guid driverProfileId, CancellationToken cancellationToken) =>
        await _db.Deliveries
            .Where(d => d.DriverProfileId == driverProfileId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Delivery delivery, CancellationToken cancellationToken) =>
        await _db.Deliveries.AddAsync(delivery, cancellationToken);

    public async Task AddEventAsync(DeliveryEvent deliveryEvent, CancellationToken cancellationToken) =>
        await _db.DeliveryEvents.AddAsync(deliveryEvent, cancellationToken);
}
