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

    public static readonly Error CpfInUse =
        new("driver.cpf_in_use", "This CPF is already registered.", ErrorType.Conflict);

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

    public static Error SubmissionIncomplete(IReadOnlyDictionary<string, string[]> missing) =>
        new("driver.submission_incomplete", "The profile is not ready to be submitted.", ErrorType.Validation, missing);
}
