using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Drivers;

public static class DriverErrors
{
    public static readonly Error ProfileNotFound =
        new("driver.profile_not_found", "Create your driver profile first.", ErrorType.NotFound);

    public static readonly Error ProfileLocked =
        new("driver.profile_locked", "The profile can only be edited while it is a draft or after a rejection.", ErrorType.Conflict);

    public static readonly Error InvalidStatusForSubmit =
        new("driver.invalid_status", "Only draft or rejected profiles can be submitted for review.", ErrorType.Conflict);

    public static readonly Error TaxIdInUse =
        new("driver.tax_id_in_use", "This CPF or CNPJ is already registered.", ErrorType.Conflict);

    public static readonly Error CnhInUse =
        new("driver.cnh_in_use", "This CNH is already registered.", ErrorType.Conflict);

    public static readonly Error VehicleNotFound =
        new("driver.vehicle_not_found", "Vehicle not found.", ErrorType.NotFound);

    public static readonly Error PlateInUse =
        new("driver.plate_in_use", "This plate is already registered.", ErrorType.Conflict);

    public static readonly Error LastVehicle =
        new("driver.last_vehicle", "A driver under review or approved must keep at least one active vehicle.", ErrorType.Conflict);

    public static readonly Error InvalidStorageKey =
        new("driver.invalid_storage_key", "The storage key must belong to the authenticated driver.", ErrorType.Validation);

    public static readonly Error StorageKeyInUse =
        new("driver.storage_key_in_use", "This file was already registered.", ErrorType.Conflict);

    public static readonly Error StorageNotConfigured =
        new("storage.not_configured", "File storage is not configured in this environment.", ErrorType.Conflict);

    public static readonly Error FileNotUploaded =
        new("driver.file_not_uploaded", "No file was uploaded under this storage key. Upload it with the signed URL first.", ErrorType.Validation);

    public static readonly Error FileTooLarge =
        new("driver.file_too_large", "The uploaded file is larger than 10 MB.", ErrorType.Validation);

    public static readonly Error FileTypeNotAllowed =
        new("driver.file_type_not_allowed", "The uploaded file must be a JPEG, PNG or PDF.", ErrorType.Validation);

    public static Error SubmissionIncomplete(IReadOnlyDictionary<string, string[]> missing) =>
        new("driver.submission_incomplete", "The profile is not ready to be submitted.", ErrorType.Validation, missing);
}
