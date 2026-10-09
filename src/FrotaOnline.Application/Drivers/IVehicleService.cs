using FrotaOnline.Application.Common;
using FrotaOnline.Application.Drivers.Dtos;

namespace FrotaOnline.Application.Drivers;

public interface IVehicleService
{
    Task<Result<IReadOnlyList<VehicleResponse>>> ListAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<VehicleResponse>> RegisterAsync(Guid userId, RegisterVehicleRequest request, CancellationToken cancellationToken);

    Task<Result> DeactivateAsync(Guid userId, Guid vehicleId, CancellationToken cancellationToken);
}
