using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Drivers.Dtos;

public sealed record UpsertDriverProfileRequest(string CpfNumber, string CnhNumber, string CnhCategory);

/// <summary>What is still missing before the profile can be sent to review.</summary>
public sealed record SubmissionChecklist(bool TermsAccepted, bool HasActiveVehicle, bool HasDocument)
{
    public bool IsReady => TermsAccepted && HasActiveVehicle && HasDocument;
}

public sealed record DriverProfileResponse(
    Guid Id,
    string CpfNumber,
    string CnhNumber,
    string CnhCategory,
    DriverStatus Status,
    string? RejectionReason,
    DateTime? ReviewedAt,
    SubmissionChecklist Checklist);

public sealed record RegisterVehicleRequest(VehicleType Type, string Plate, string Model, int Year, decimal CapacityKg);

public sealed record VehicleResponse(Guid Id, VehicleType Type, string Plate, string Model, int Year, decimal CapacityKg, bool IsActive);

public sealed record RegisterMediaRequest(MediaKind Kind, string StorageKey, string ContentType, long SizeBytes);

public sealed record MediaFileResponse(Guid Id, MediaKind Kind, string StorageKey, string ContentType, long SizeBytes, DateTime CreatedAt);
