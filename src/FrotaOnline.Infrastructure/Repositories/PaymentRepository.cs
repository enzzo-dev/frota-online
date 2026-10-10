using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class PaymentRepository : IPaymentRepository
{
    private readonly AppDbContext _db;

    public PaymentRepository(AppDbContext db) => _db = db;

    public Task<bool> ExistsForDeliveryAsync(Guid deliveryId, CancellationToken cancellationToken) =>
        _db.Payments.AnyAsync(p => p.DeliveryId == deliveryId, cancellationToken);

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken) =>
        await _db.Payments.AddAsync(payment, cancellationToken);
}
