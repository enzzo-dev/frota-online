using FrotaOnline.Application.Drivers.Dtos;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Admin.Dtos;

public sealed record DriverReviewSummary(
    Guid DriverId,
    Guid UserId,
    string FullName,
    string Email,
    string PhoneNumber,
    DriverStatus Status,
    DateTime? LastUpdatedAt);

public sealed record DriverReviewDetail(
    Guid DriverId,
    Guid UserId,
    string FullName,
    string Email,
    string PhoneNumber,
    string TaxId,
    string CnhNumber,
    string CnhCategory,
    DriverStatus Status,
    string? RejectionReason,
    DateTime? ReviewedAt,
    IReadOnlyList<VehicleResponse> Vehicles,
    IReadOnlyList<MediaFileResponse> Files);

public sealed record RejectDriverRequest(string Reason);
