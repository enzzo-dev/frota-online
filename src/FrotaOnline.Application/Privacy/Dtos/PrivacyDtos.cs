using FrotaOnline.Application.Addresses.Dtos;
using FrotaOnline.Application.Drivers.Dtos;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Privacy.Dtos;

/// <summary>Password confirms it is really the owner; Confirm must be true so a stray call cannot erase an account.</summary>
public sealed record DeleteAccountRequest(string Password, bool Confirm);

public sealed record AccountDeletionResponse(DateTime DeletedAt, IReadOnlyList<string> RetainedData);

public sealed record PersonalAccountData(
    Guid Id, string FullName, string Email, string PhoneNumber, UserRole Role, DateTime CreatedAt, DateTime? LastLoginAt);

public sealed record PersonalDriverData(
    string TaxId, string CnhNumber, string CnhCategory, DriverStatus Status, string? RejectionReason, DateTime? ReviewedAt);

public sealed record PersonalConsentData(TermType TermType, string Version, DateTime AcceptedAt, string? IpAddress);

public sealed record PersonalFreightData(
    Guid Id, FreightStatus Status, string CargoDescription, decimal CargoWeightKg, int StopCount, DateTime CreatedAt);

public sealed record PersonalProposalData(
    Guid Id, Guid FreightRequestId, decimal Price, string? Message, ProposalStatus Status, DateTime CreatedAt);

/// <summary>Role tells whether the person was the customer or the driver of the delivery.</summary>
public sealed record PersonalDeliveryData(
    Guid Id, Guid FreightRequestId, string Role, DeliveryStatus Status, decimal AgreedPrice, decimal PlatformFee, DateTime CreatedAt);

/// <summary>Everything the platform holds about the signed-in person (LGPD art. 18, II and V).</summary>
public sealed record PersonalDataExport(
    DateTime GeneratedAt,
    PersonalAccountData Account,
    string? CustomerDocumentNumber,
    PersonalDriverData? Driver,
    IReadOnlyList<VehicleResponse> Vehicles,
    IReadOnlyList<AddressResponse> Addresses,
    IReadOnlyList<MediaFileResponse> Files,
    IReadOnlyList<PersonalConsentData> Consents,
    IReadOnlyList<PersonalFreightData> Freights,
    IReadOnlyList<PersonalProposalData> Proposals,
    IReadOnlyList<PersonalDeliveryData> Deliveries);
