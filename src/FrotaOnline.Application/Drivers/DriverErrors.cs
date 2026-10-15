using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Drivers;

public static class DriverErrors
{
    public static readonly Error ProfileNotFound =
        new("driver.profile_not_found", "Crie primeiro o seu perfil de motorista.", ErrorType.NotFound);

    public static readonly Error ProfileLocked =
        new("driver.profile_locked", "O perfil só pode ser editado enquanto é rascunho ou depois de uma reprovação.", ErrorType.Conflict);

    public static readonly Error InvalidStatusForSubmit =
        new("driver.invalid_status", "Só perfis em rascunho ou reprovados podem ser enviados para análise.", ErrorType.Conflict);

    public static readonly Error TaxIdInUse =
        new("driver.tax_id_in_use", "Este CPF ou CNPJ já está cadastrado.", ErrorType.Conflict);

    public static readonly Error CnhInUse =
        new("driver.cnh_in_use", "Esta CNH já está cadastrada.", ErrorType.Conflict);

    public static readonly Error VehicleNotFound =
        new("driver.vehicle_not_found", "Veículo não encontrado.", ErrorType.NotFound);

    public static readonly Error PlateInUse =
        new("driver.plate_in_use", "Esta placa já está cadastrada.", ErrorType.Conflict);

    public static readonly Error LastVehicle =
        new("driver.last_vehicle", "Um motorista em análise ou aprovado deve manter ao menos um veículo ativo.", ErrorType.Conflict);

    public static readonly Error InvalidStorageKey =
        new("driver.invalid_storage_key", "A chave do arquivo deve pertencer ao motorista autenticado.", ErrorType.Validation);

    public static readonly Error StorageKeyInUse =
        new("driver.storage_key_in_use", "Este arquivo já foi registrado.", ErrorType.Conflict);

    public static readonly Error StorageNotConfigured =
        new("storage.not_configured", "O armazenamento de arquivos não está configurado neste ambiente.", ErrorType.Conflict);

    public static readonly Error FileNotUploaded =
        new("driver.file_not_uploaded", "Nenhum arquivo foi enviado com esta chave. Envie-o primeiro pela URL assinada.", ErrorType.Validation);

    public static readonly Error FileTooLarge =
        new("driver.file_too_large", "O arquivo enviado é maior que 10 MB.", ErrorType.Validation);

    public static readonly Error FileTypeNotAllowed =
        new("driver.file_type_not_allowed", "O arquivo enviado deve ser JPEG, PNG ou PDF.", ErrorType.Validation);

    public static Error SubmissionIncomplete(IReadOnlyDictionary<string, string[]> missing) =>
        new("driver.submission_incomplete", "O perfil ainda não está pronto para ser enviado.", ErrorType.Validation, missing);
}
