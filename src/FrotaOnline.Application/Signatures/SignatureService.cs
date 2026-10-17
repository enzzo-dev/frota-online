using FluentValidation;
using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Storage;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Marketplace;
using FrotaOnline.Application.Signatures.Dtos;
using FrotaOnline.Application.Signatures.Validators;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Signatures;

public sealed class SignatureService : ISignatureService
{
    private static readonly TimeSpan UploadLifetime = TimeSpan.FromMinutes(15);

    private readonly IDeliveryRepository _deliveries;
    private readonly IDriverRepository _drivers;
    private readonly ISignatureRepository _signatures;
    private readonly IFileStorage _storage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateSignatureUploadUrlRequest> _uploadValidator;
    private readonly IValidator<RegisterSignatureRequest> _registerValidator;

    public SignatureService(
        IDeliveryRepository deliveries,
        IDriverRepository drivers,
        ISignatureRepository signatures,
        IFileStorage storage,
        IUnitOfWork unitOfWork,
        IValidator<CreateSignatureUploadUrlRequest> uploadValidator,
        IValidator<RegisterSignatureRequest> registerValidator)
    {
        _deliveries = deliveries;
        _drivers = drivers;
        _signatures = signatures;
        _storage = storage;
        _unitOfWork = unitOfWork;
        _uploadValidator = uploadValidator;
        _registerValidator = registerValidator;
    }

    public async Task<Result<SignatureUploadUrlResponse>> CreateUploadUrlAsync(
        Guid userId, CreateSignatureUploadUrlRequest request, CancellationToken cancellationToken)
    {
        var validation = await _uploadValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<SignatureUploadUrlResponse>(ValidationMapper.ToError(validation));
        }

        var delivery = await ResolveDeliveryAsync(userId, request.Type, request.FreightId, cancellationToken);
        if (!delivery.IsSuccess)
        {
            return Result.Failure<SignatureUploadUrlResponse>(delivery.Error!);
        }

        if (!_storage.IsConfigured)
        {
            return Result.Failure<SignatureUploadUrlResponse>(SignatureErrors.StorageNotConfigured);
        }

        if (await _signatures.ExistsAsync(request.FreightId, request.Type, cancellationToken))
        {
            return Result.Failure<SignatureUploadUrlResponse>(SignatureErrors.AlreadyCollected);
        }

        var path = $"{Prefix(request.FreightId, request.Type)}{Guid.NewGuid():N}{Extension(request.ContentType)}";
        var ticket = await _storage.CreateUploadAsync(
            path, request.ContentType, SignatureLimits.MaxSizeBytes, UploadLifetime, cancellationToken);

        return Result.Success(new SignatureUploadUrlResponse(
            path, ticket.Url, "PUT", ticket.Headers, SignatureLimits.MaxSizeBytes, ticket.ExpiresAt));
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

        if (!request.StoragePath.StartsWith(Prefix(request.FreightId, request.Type), StringComparison.Ordinal)
            || request.StoragePath.Contains(".."))
        {
            return Result.Failure<SignatureResponse>(SignatureErrors.InvalidStoragePath);
        }

        if (await _signatures.ExistsAsync(request.FreightId, request.Type, cancellationToken))
        {
            return Result.Failure<SignatureResponse>(SignatureErrors.AlreadyCollected);
        }

        if (await _signatures.StoragePathExistsAsync(request.StoragePath, cancellationToken))
        {
            return Result.Failure<SignatureResponse>(SignatureErrors.StoragePathInUse);
        }

        if (_storage.IsConfigured)
        {
            var stored = await _storage.GetInfoAsync(request.StoragePath, cancellationToken);
            if (stored is null)
            {
                return Result.Failure<SignatureResponse>(SignatureErrors.FileNotUploaded);
            }

            if (stored.SizeBytes <= 0 || stored.SizeBytes > SignatureLimits.MaxSizeBytes)
            {
                await _storage.DeleteAsync(new[] { request.StoragePath }, cancellationToken);
                return Result.Failure<SignatureResponse>(SignatureErrors.FileTooLarge);
            }

            if (!SignatureLimits.AllowedContentTypes.Contains(stored.ContentType))
            {
                await _storage.DeleteAsync(new[] { request.StoragePath }, cancellationToken);
                return Result.Failure<SignatureResponse>(SignatureErrors.FileTypeNotAllowed);
            }
        }

        var delivery = resolved.Value;
        var signature = Signature.Create(
            request.Type, delivery.FreightRequestId, delivery.CustomerId, delivery.DriverProfileId, request.StoragePath);
        await _signatures.AddAsync(signature, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new SignatureResponse(
            signature.Id, signature.Type, signature.FreightRequestId, signature.CustomerId,
            signature.DriverProfileId, signature.StoragePath, signature.CreatedAt));
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

        var driver = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        return driver is not null && driver.Id == delivery.DriverProfileId;
    }

    private static string Prefix(Guid freightId, SignatureType type) =>
        $"signatures/{freightId:N}/{type.ToString().ToLowerInvariant()}/";

    private static string Extension(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        _ => string.Empty
    };
}
