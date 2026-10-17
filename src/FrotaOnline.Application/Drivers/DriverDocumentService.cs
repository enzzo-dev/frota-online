using FluentValidation;
using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Storage;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Drivers.Dtos;
using FrotaOnline.Application.Drivers.Validators;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Drivers;

public sealed class DriverDocumentService : IDriverDocumentService
{
    private static readonly MediaKind[] DriverKinds = { MediaKind.DriverDocument, MediaKind.VehiclePhoto};
    private static readonly TimeSpan UploadLifetime = TimeSpan.FromMinutes(15);

    private readonly IDriverRepository _drivers;
    private readonly IMediaFileRepository _media;
    private readonly IFileStorage _storage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<RegisterMediaRequest> _validator;
    private readonly IValidator<CreateUploadUrlRequest> _uploadValidator;

    public DriverDocumentService(
        IDriverRepository drivers,
        IMediaFileRepository media,
        IFileStorage storage,
        IUnitOfWork unitOfWork,
        IValidator<RegisterMediaRequest> validator,
        IValidator<CreateUploadUrlRequest> uploadValidator)
    {
        _drivers = drivers;
        _media = media;
        _storage = storage;
        _unitOfWork = unitOfWork;
        _validator = validator;
        _uploadValidator = uploadValidator;
    }

    public async Task<Result<IReadOnlyList<MediaFileResponse>>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var files = await _media.ListByOwnerAsync(userId, DriverKinds, cancellationToken);
        return Result.Success<IReadOnlyList<MediaFileResponse>>(files.Select(ToResponse).ToList());
    }

    public async Task<Result<UploadUrlResponse>> CreateUploadUrlAsync(
        Guid userId, CreateUploadUrlRequest request, CancellationToken cancellationToken)
    {
        var validation = await _uploadValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure<UploadUrlResponse>(ValidationMapper.ToError(validation));

        if (!_storage.IsConfigured)
            return Result.Failure<UploadUrlResponse>(DriverErrors.StorageNotConfigured);

        if (await _drivers.GetByUserIdAsync(userId, cancellationToken) is null)
            return Result.Failure<UploadUrlResponse>(DriverErrors.ProfileNotFound);

        var key = $"drivers/{userId}/{Guid.NewGuid():N}{Extension(request.ContentType)}";
        var ticket = await _storage.CreateUploadAsync(
            key, request.ContentType, RegisterMediaRequestValidator.MaxSizeBytes, UploadLifetime, cancellationToken);

        return Result.Success(new UploadUrlResponse(
            key, ticket.Url, "PUT", ticket.Headers, RegisterMediaRequestValidator.MaxSizeBytes, ticket.ExpiresAt));
    }

    public async Task<Result<MediaFileResponse>> RegisterAsync(
        Guid userId, RegisterMediaRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<MediaFileResponse>(ValidationMapper.ToError(validation));
        }

        if (await _drivers.GetByUserIdAsync(userId, cancellationToken) is null)
        {
            return Result.Failure<MediaFileResponse>(DriverErrors.ProfileNotFound);
        }

        if (!request.StorageKey.StartsWith($"drivers/{userId}/", StringComparison.Ordinal)
            || request.StorageKey.Contains(".."))
        {
            return Result.Failure<MediaFileResponse>(DriverErrors.InvalidStorageKey);
        }

        if (await _media.StorageKeyExistsAsync(request.StorageKey, cancellationToken))
        {
            return Result.Failure<MediaFileResponse>(DriverErrors.StorageKeyInUse);
        }

        var contentType = request.ContentType;
        var size = request.SizeBytes;

        // With storage configured, the object itself is the truth: what the client says about it is not trusted.
        if (_storage.IsConfigured)
        {
            var stored = await _storage.GetInfoAsync(request.StorageKey, cancellationToken);
            if (stored is null)
            {
                return Result.Failure<MediaFileResponse>(DriverErrors.FileNotUploaded);
            }

            if (stored.SizeBytes <= 0 || stored.SizeBytes > RegisterMediaRequestValidator.MaxSizeBytes)
            {
                await _storage.DeleteAsync(new[] { request.StorageKey }, cancellationToken);
                return Result.Failure<MediaFileResponse>(DriverErrors.FileTooLarge);
            }

            if (!RegisterMediaRequestValidator.AllowedContentTypes.Contains(stored.ContentType))
            {
                await _storage.DeleteAsync(new[] { request.StorageKey }, cancellationToken);
                return Result.Failure<MediaFileResponse>(DriverErrors.FileTypeNotAllowed);
            }

            contentType = stored.ContentType;
            size = stored.SizeBytes;
        }

        var file = MediaFile.Create(userId, request.Kind, request.StorageKey, contentType, size);
        await _media.AddAsync(file, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ToResponse(file));
    }

    private static string Extension(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "application/pdf" => ".pdf",
        _ => string.Empty
    };

    private static MediaFileResponse ToResponse(MediaFile f) =>
        new(f.Id, f.Kind, f.StorageKey, f.ContentType, f.SizeBytes, f.CreatedAt);
}
