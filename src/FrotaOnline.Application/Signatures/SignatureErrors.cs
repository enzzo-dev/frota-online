using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Signatures;

public static class SignatureErrors
{
    public static readonly Error AlreadyCollected =
        new("signature.already_collected", "Esta assinatura já foi coletada para o frete.", ErrorType.Conflict);

    public static readonly Error InvalidStoragePath =
        new("signature.invalid_storage_path", "O caminho do arquivo não pertence a esta assinatura.", ErrorType.Validation);

    public static readonly Error StoragePathInUse =
        new("signature.storage_path_in_use", "Este arquivo já foi registrado.", ErrorType.Conflict);

    public static readonly Error StorageNotConfigured =
        new("storage.not_configured", "O armazenamento de arquivos não está configurado neste ambiente.", ErrorType.Conflict);

    public static readonly Error FileNotUploaded =
        new("signature.file_not_uploaded", "Nenhum arquivo foi enviado neste caminho. Envie-o primeiro pela URL assinada.", ErrorType.Validation);

    public static readonly Error FileTooLarge =
        new("signature.file_too_large", "A imagem da assinatura é maior que 2 MB.", ErrorType.Validation);

    public static readonly Error FileTypeNotAllowed =
        new("signature.file_type_not_allowed", "A assinatura deve ser uma imagem PNG ou JPEG.", ErrorType.Validation);

    public static readonly Error DeliveryCancelled =
        new("signature.delivery_cancelled", "A entrega foi cancelada; não é possível coletar assinaturas.", ErrorType.Conflict);
}
