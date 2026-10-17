using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Drivers.Dtos;

public sealed record UpsertDriverProfileRequest(string TaxId, string CnhNumber, string CnhCategory);

/// <summary>What is still missing before the profile can be sent to review.</summary>
public sealed record SubmissionChecklist(bool TermsAccepted, bool HasActiveVehicle, bool HasDocument)
{
    public bool IsReady => TermsAccepted && HasActiveVehicle && HasDocument;
}

public sealed record DriverProfileResponse(
    Guid Id,
    string TaxId,
    string CnhNumber,
    string CnhCategory,
    DriverStatus Status,
    string? RejectionReason,
    DateTime? ReviewedAt,
    SubmissionChecklist Checklist);

public sealed record RegisterVehicleRequest(VehicleType Type, string Plate, string Model, int Year, decimal CapacityKg);

#region Vehicle
public sealed record VehicleResponse(Guid Id, VehicleType Type, string Plate, string Model, int Year, decimal CapacityKg, bool IsActive);

#endregion

/// <summary>Asks for a signed URL to upload one file. The content type is the one the upload will carry.</summary>
public sealed record CreateUploadUrlRequest(MediaKind Kind, string ContentType);

/// <summary>Upload with HTTP PUT to <paramref name="UploadUrl"/> sending <paramref name="Headers"/> exactly, then register the key.</summary>
public sealed record UploadUrlResponse(
    string StorageKey, string UploadUrl, string Method, IReadOnlyDictionary<string, string> Headers, long MaxBytes, DateTime ExpiresAt);

public sealed record DownloadUrlResponse(string Url, DateTime ExpiresAt);

public sealed record RegisterMediaRequest(MediaKind Kind, string StorageKey, string ContentType, long SizeBytes);

public sealed record MediaFileResponse(Guid Id, MediaKind Kind, string StorageKey, string ContentType, long SizeBytes, DateTime CreatedAt);
