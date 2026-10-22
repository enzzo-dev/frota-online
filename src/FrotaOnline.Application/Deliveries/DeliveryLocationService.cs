using FluentValidation;
using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Deliveries.Dtos;
using FrotaOnline.Application.Drivers;
using FrotaOnline.Application.Marketplace;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Deliveries;

public sealed class DeliveryLocationService : IDeliveryLocationService
{
    private readonly IDriverRepository _drivers;
    private readonly IDeliveryRepository _deliveries;
    private readonly IDeliveryLocationRepository _locations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<UpdateLocationRequest> _validator;

    public DeliveryLocationService(
        IDriverRepository drivers,
        IDeliveryRepository deliveries,
        IDeliveryLocationRepository locations,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IValidator<UpdateLocationRequest> validator)
    {
        _drivers = drivers;
        _deliveries = deliveries;
        _locations = locations;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _validator = validator;
    }

    public async Task<Result> UpdateAsync(
        Guid userId, Guid deliveryId, UpdateLocationRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(ValidationMapper.ToError(validation));
        }

        var now = _clock.UtcNow;
        var reported = DeliveryLocation.Create(deliveryId, request.Latitude, request.Longitude, request.Heading, request.SpeedKmh, now);
        if (await _locations.TryRecordForDriverAsync(userId, reported, cancellationToken))
        {
            return Result.Success();
        }

        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure(DriverErrors.ProfileNotFound);
        }

        var delivery = await _deliveries.GetByIdAsync(deliveryId, cancellationToken);
        if (delivery is null || delivery.DriverProfileId != profile.Id)
        {
            return Result.Failure(MarketplaceErrors.DeliveryNotFound);
        }

        if (delivery.Status is not (DeliveryStatus.ToPickup or DeliveryStatus.PickedUp or DeliveryStatus.InTransit))
        {
            return Result.Failure(DeliveryErrors.InvalidStatus(
                $"A entrega está com status {delivery.Status}; a posição só é aceita enquanto ela está em andamento."));
        }

        var current = await _locations.GetByDeliveryIdAsync(deliveryId, cancellationToken);
        if (current is null)
        {
            await _locations.AddAsync(reported, cancellationToken);
        }
        else
        {
            current.MoveTo(request.Latitude, request.Longitude, request.Heading, request.SpeedKmh, now);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
