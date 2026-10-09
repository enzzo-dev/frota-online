using FluentValidation;
using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Drivers.Dtos;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Drivers;

public sealed class VehicleService : IVehicleService
{
    private readonly IDriverRepository _drivers;
    private readonly IVehicleRepository _vehicles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<RegisterVehicleRequest> _validator;

    public VehicleService(
        IDriverRepository drivers,
        IVehicleRepository vehicles,
        IUnitOfWork unitOfWork,
        IValidator<RegisterVehicleRequest> validator)
    {
        _drivers = drivers;
        _vehicles = vehicles;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<Result<IReadOnlyList<VehicleResponse>>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<IReadOnlyList<VehicleResponse>>(DriverErrors.ProfileNotFound);
        }

        var vehicles = await _vehicles.ListActiveByDriverAsync(profile.Id, cancellationToken);
        return Result.Success<IReadOnlyList<VehicleResponse>>(vehicles.Select(ToResponse).ToList());
    }

    public async Task<Result<VehicleResponse>> RegisterAsync(
        Guid userId, RegisterVehicleRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<VehicleResponse>(ValidationMapper.ToError(validation));
        }

        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<VehicleResponse>(DriverErrors.ProfileNotFound);
        }

        var plate = BrazilianDocuments.NormalizePlate(request.Plate);
        if (await _vehicles.PlateInUseAsync(plate, cancellationToken))
        {
            return Result.Failure<VehicleResponse>(DriverErrors.PlateInUse);
        }

        var vehicle = Vehicle.Create(profile.Id, request.Type, plate, request.Model, request.Year, request.CapacityKg);
        await _vehicles.AddAsync(vehicle, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ToResponse(vehicle));
    }

    public async Task<Result> DeactivateAsync(Guid userId, Guid vehicleId, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure(DriverErrors.ProfileNotFound);
        }

        var vehicle = await _vehicles.GetByIdAsync(vehicleId, cancellationToken);
        if (vehicle is null || vehicle.DriverProfileId != profile.Id || !vehicle.IsActive)
        {
            return Result.Failure(DriverErrors.VehicleNotFound);
        }

        if (profile.Status is DriverStatus.Approved or DriverStatus.PendingReview)
        {
            var active = await _vehicles.ListActiveByDriverAsync(profile.Id, cancellationToken);
            if (active.Count <= 1)
            {
                return Result.Failure(DriverErrors.LastVehicle);
            }
        }

        vehicle.Deactivate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static VehicleResponse ToResponse(Vehicle v) =>
        new(v.Id, v.Type, v.Plate, v.Model, v.Year, v.CapacityKg, v.IsActive);
}
