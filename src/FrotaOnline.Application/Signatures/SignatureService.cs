using FluentValidation;
using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Marketplace;
using FrotaOnline.Application.Signatures.Dtos;
using FrotaOnline.Application.Signatures.Validators;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Signatures;

public sealed class SignatureService : ISignatureService
{
    private readonly IDeliveryRepository _deliveries;
    private readonly IDriverRepository _drivers;
    private readonly ISignatureRepository _signatures;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<RegisterSignatureRequest> _registerValidator;

    public SignatureService(
        IDeliveryRepository deliveries,
        IDriverRepository drivers,
        ISignatureRepository signatures,
        IUnitOfWork unitOfWork,
        IValidator<RegisterSignatureRequest> registerValidator)
    {
        _deliveries = deliveries;
        _drivers = drivers;
        _signatures = signatures;
        _unitOfWork = unitOfWork;
        _registerValidator = registerValidator;
    }

    public async Task<Result<SignatureResponse>> RegisterAsync(
        Guid userId, RegisterSignatureRequest request, CancellationToken cancellationToken)
    {
        var validation = await _registerValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<SignatureResponse>(ValidationMapper.ToError(validation));
        }

        var resolved = await ResolveDeliveryAsync(userId, request.Type, request.FreightId, cancellationToken);
        if (!resolved.IsSuccess)
        {
            return Result.Failure<SignatureResponse>(resolved.Error!);
        }

        if (!SignatureImageCodec.TryDecodeBase64(request.Image, out var image) || image.Length == 0)
        {
            return Result.Failure<SignatureResponse>(SignatureErrors.InvalidImage);
        }

        if (image.Length > SignatureLimits.MaxSizeBytes)
        {
            return Result.Failure<SignatureResponse>(SignatureErrors.FileTooLarge);
        }

        var contentType = SignatureImageCodec.DetectContentType(image);
        if (contentType is null)
        {
            return Result.Failure<SignatureResponse>(SignatureErrors.FileTypeNotAllowed);
        }

        if (await _signatures.ExistsAsync(request.FreightId, request.Type, cancellationToken))
        {
            return Result.Failure<SignatureResponse>(SignatureErrors.AlreadyCollected);
        }

        var delivery = resolved.Value;
        var signature = Signature.Create(
            request.Type, delivery.FreightRequestId, delivery.CustomerId, delivery.DriverProfileId,
            SignatureImageCodec.Compress(image), contentType, image.Length);
        await _signatures.AddAsync(signature, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new SignatureResponse(
            signature.Id, signature.Type, signature.FreightRequestId, signature.CustomerId,
            signature.DriverProfileId, signature.ContentType, signature.SizeBytes, signature.CreatedAt));
    }

    public async Task<Result<SignatureImage>> GetImageAsync(
        Guid userId, Guid freightId, SignatureType type, CancellationToken cancellationToken)
    {
        var delivery = await _deliveries.GetByFreightRequestIdAsync(freightId, cancellationToken);
        if (delivery is null || !await IsParticipantAsync(userId, delivery, cancellationToken))
        {
            return Result.Failure<SignatureImage>(MarketplaceErrors.DeliveryNotFound);
        }

        var signature = await _signatures.GetAsync(freightId, type, cancellationToken);
        return signature is null
            ? Result.Failure<SignatureImage>(SignatureErrors.NotFound)
            : Result.Success(new SignatureImage(signature.ContentType, SignatureImageCodec.Decompress(signature.ImageData)));
    }

    private async Task<Result<Delivery>> ResolveDeliveryAsync(
        Guid userId, SignatureType type, Guid freightId, CancellationToken cancellationToken)
    {
        var delivery = await _deliveries.GetByFreightRequestIdAsync(freightId, cancellationToken);
        if (delivery is null || !await SignsAsAsync(userId, type, delivery, cancellationToken))
        {
            return Result.Failure<Delivery>(MarketplaceErrors.DeliveryNotFound);
        }

        return delivery.Status == DeliveryStatus.Cancelled
            ? Result.Failure<Delivery>(SignatureErrors.DeliveryCancelled)
            : Result.Success(delivery);
    }

    private async Task<bool> SignsAsAsync(Guid userId, SignatureType type, Delivery delivery, CancellationToken cancellationToken)
    {
        if (type == SignatureType.Customer && delivery.CustomerId == userId)
        {
            return true;
        }

        return await IsDriverOfAsync(userId, delivery, cancellationToken);
    }

    private async Task<bool> IsParticipantAsync(Guid userId, Delivery delivery, CancellationToken cancellationToken) =>
        delivery.CustomerId == userId || await IsDriverOfAsync(userId, delivery, cancellationToken);

    private async Task<bool> IsDriverOfAsync(Guid userId, Delivery delivery, CancellationToken cancellationToken)
    {
        var driver = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        return driver is not null && driver.Id == delivery.DriverProfileId;
    }
}
