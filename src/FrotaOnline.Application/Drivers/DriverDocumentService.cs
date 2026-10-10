using FluentValidation;
using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Drivers.Dtos;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Drivers;

public sealed class DriverDocumentService : IDriverDocumentService
{
    private static readonly MediaKind[] DriverKinds = { MediaKind.DriverDocument, MediaKind.VehiclePhoto, MediaKind.DeliveryProof };

    private readonly IDriverRepository _drivers;
    private readonly IMediaFileRepository _media;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<RegisterMediaRequest> _validator;

    public DriverDocumentService(
        IDriverRepository drivers,
        IMediaFileRepository media,
        IUnitOfWork unitOfWork,
        IValidator<RegisterMediaRequest> validator)
    {
        _drivers = drivers;
        _media = media;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<Result<IReadOnlyList<MediaFileResponse>>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var files = await _media.ListByOwnerAsync(userId, DriverKinds, cancellationToken);
        return Result.Success<IReadOnlyList<MediaFileResponse>>(files.Select(ToResponse).ToList());
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

        var file = MediaFile.Create(userId, request.Kind, request.StorageKey, request.ContentType, request.SizeBytes);
        await _media.AddAsync(file, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ToResponse(file));
    }

    private static MediaFileResponse ToResponse(MediaFile f) =>
        new(f.Id, f.Kind, f.StorageKey, f.ContentType, f.SizeBytes, f.CreatedAt);
}
