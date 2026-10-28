using FluentValidation;
using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Application.Signatures;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Marketplace;

public sealed class CargoPhotoService : ICargoPhotoService
{
    private readonly IFreightRepository _freights;
    private readonly IDeliveryRepository _deliveries;
    private readonly IDriverRepository _drivers;
    private readonly ICargoPhotoRepository _photos;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<UploadCargoPhotoRequest> _validator;

    public CargoPhotoService(
        IFreightRepository freights,
        IDeliveryRepository deliveries,
        IDriverRepository drivers,
        ICargoPhotoRepository photos,
        IUnitOfWork unitOfWork,
        IValidator<UploadCargoPhotoRequest> validator)
    {
        _freights = freights;
        _deliveries = deliveries;
        _drivers = drivers;
        _photos = photos;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<Result> UploadAsync(
        Guid customerId, Guid freightId, UploadCargoPhotoRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(ValidationMapper.ToError(validation));
        }

        var freight = await _freights.GetByIdAsync(freightId, cancellationToken);
        if (freight is null || freight.CustomerId != customerId)
        {
            return Result.Failure(MarketplaceErrors.FreightNotFound);
        }

        if (freight.Status != FreightStatus.Open)
        {
            return Result.Failure(MarketplaceErrors.FreightNotOpen);
        }

        if (!SignatureImageCodec.TryDecodeBase64(request.Image, out var image) || image.Length == 0)
        {
            return Result.Failure(CargoPhotoErrors.InvalidImage);
        }

        if (image.Length > CargoPhotoLimits.MaxSizeBytes)
        {
            return Result.Failure(CargoPhotoErrors.FileTooLarge);
        }

        var contentType = SignatureImageCodec.DetectContentType(image);
        if (contentType is null)
        {
            return Result.Failure(CargoPhotoErrors.FileTypeNotAllowed);
        }

        if (await _photos.ExistsAsync(freightId, cancellationToken))
        {
            return Result.Failure(CargoPhotoErrors.AlreadyExists);
        }

        await _photos.AddAsync(
            FreightCargoPhoto.Create(freightId, SignatureImageCodec.Compress(image), contentType, image.Length),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<CargoPhotoImage>> GetAsync(Guid userId, Guid freightId, CancellationToken cancellationToken)
    {
        var freight = await _freights.GetByIdAsync(freightId, cancellationToken);
        if (freight is null || !await CanSeeAsync(userId, freight, cancellationToken))
        {
            return Result.Failure<CargoPhotoImage>(MarketplaceErrors.FreightNotFound);
        }

        var photo = await _photos.GetAsync(freightId, cancellationToken);
        return photo is null
            ? Result.Failure<CargoPhotoImage>(CargoPhotoErrors.NotFound)
            : Result.Success(new CargoPhotoImage(photo.ContentType, SignatureImageCodec.Decompress(photo.ImageData)));
    }

    private async Task<bool> CanSeeAsync(Guid userId, FreightRequest freight, CancellationToken cancellationToken)
    {
        if (freight.CustomerId == userId)
        {
            return true;
        }

        var driver = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (driver is null)
        {
            return false;
        }

        var delivery = await _deliveries.GetByFreightRequestIdAsync(freight.Id, cancellationToken);
        if (delivery is not null)
        {
            return delivery.DriverProfileId == driver.Id;
        }

        return driver.CanOperate && freight.Status == FreightStatus.Open;
    }
}
