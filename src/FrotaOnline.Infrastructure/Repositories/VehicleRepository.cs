using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class VehicleRepository : IVehicleRepository
{
    private readonly AppDbContext _db;

    public VehicleRepository(AppDbContext db) => _db = db;

    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Vehicle>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await _db.Vehicles.Where(v => ids.Contains(v.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Vehicle>> ListActiveByDriverAsync(Guid driverProfileId, CancellationToken cancellationToken) =>
        await _db.Vehicles
            .Where(v => v.DriverProfileId == driverProfileId && v.IsActive)
            .OrderBy(v => v.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<bool> PlateInUseAsync(string plate, CancellationToken cancellationToken) =>
        _db.Vehicles.AnyAsync(v => v.Plate == plate, cancellationToken);

    public async Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken) =>
        await _db.Vehicles.AddAsync(vehicle, cancellationToken);
}
