using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Marketplace;

public static class CargoPhotoErrors
{
    public static readonly Error AlreadyExists =
        new("cargo_photo.already_exists", "Este frete já tem foto da carga.", ErrorType.Conflict);

    public static readonly Error InvalidImage =
        new("cargo_photo.invalid_image", "A foto da carga não está em base64 válido.", ErrorType.Validation);

    public static readonly Error FileTooLarge =
        new("cargo_photo.file_too_large", "A foto da carga é maior que 2 MB.", ErrorType.Validation);

    public static readonly Error FileTypeNotAllowed =
        new("cargo_photo.file_type_not_allowed", "A foto da carga deve ser uma imagem PNG ou JPEG.", ErrorType.Validation);

    public static readonly Error NotFound =
        new("cargo_photo.not_found", "Este frete não tem foto da carga.", ErrorType.NotFound);
}
