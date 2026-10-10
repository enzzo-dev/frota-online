using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Infrastructure.Persistence;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class DeliveryRepository : IDeliveryRepository
{
    private readonly AppDbContext _db;

    public DeliveryRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(Delivery delivery, CancellationToken cancellationToken) =>
        await _db.Deliveries.AddAsync(delivery, cancellationToken);

    public async Task AddEventAsync(DeliveryEvent deliveryEvent, CancellationToken cancellationToken) =>
        await _db.DeliveryEvents.AddAsync(deliveryEvent, cancellationToken);
}
