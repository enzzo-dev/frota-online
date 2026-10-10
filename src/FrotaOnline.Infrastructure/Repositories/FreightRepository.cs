using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class FreightRepository : IFreightRepository
{
    private const int OpenListLimit = 100;

    private readonly AppDbContext _db;

    public FreightRepository(AppDbContext db) => _db = db;

    public Task<FreightRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.FreightRequests.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task<IReadOnlyList<FreightRequest>> ListByCustomerAsync(
        Guid customerId, FreightStatus? status, CancellationToken cancellationToken)
    {
        var query = _db.FreightRequests.Where(f => f.CustomerId == customerId);
        if (status is not null)
        {
            query = query.Where(f => f.Status == status);
        }

        return await query.OrderByDescending(f => f.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FreightRequest>> ListOpenAsync(
        DateTime utcNow, IReadOnlyCollection<VehicleType> vehicleTypes, CancellationToken cancellationToken) =>
        await _db.FreightRequests
            .Where(f => f.Status == FreightStatus.Open
                        && f.ProposalDeadline > utcNow
                        && vehicleTypes.Contains(f.RequiredVehicleType))
            .OrderByDescending(f => f.CreatedAt)
            .Take(OpenListLimit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FreightRequest>> ListOpenDueAsync(DateTime utcNow, CancellationToken cancellationToken) =>
        await _db.FreightRequests
            .Where(f => f.Status == FreightStatus.Open && f.ProposalDeadline <= utcNow)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(FreightRequest freight, CancellationToken cancellationToken) =>
        await _db.FreightRequests.AddAsync(freight, cancellationToken);
}
