using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Abstractions.Drivers;

public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Vehicle>> ListActiveByDriverAsync(Guid driverProfileId, CancellationToken cancellationToken);

    Task<bool> PlateInUseAsync(string plate, CancellationToken cancellationToken);

    Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken);
}
