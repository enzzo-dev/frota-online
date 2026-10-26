using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Signatures;

public static class SignatureErrors
{
    public static readonly Error AlreadyCollected =
        new("signature.already_collected", "Esta assinatura já foi coletada para o frete.", ErrorType.Conflict);

    public static readonly Error InvalidImage =
        new("signature.invalid_image", "A imagem da assinatura não está em base64 válido.", ErrorType.Validation);

    public static readonly Error FileTooLarge =
        new("signature.file_too_large", "A imagem da assinatura é maior que 2 MB.", ErrorType.Validation);

    public static readonly Error FileTypeNotAllowed =
        new("signature.file_type_not_allowed", "A assinatura deve ser uma imagem PNG ou JPEG.", ErrorType.Validation);

    public static readonly Error DeliveryCancelled =
        new("signature.delivery_cancelled", "A entrega foi cancelada; não é possível coletar assinaturas.", ErrorType.Conflict);

    public static readonly Error NotFound =
        new("signature.not_found", "Assinatura não encontrada.", ErrorType.NotFound);
}
