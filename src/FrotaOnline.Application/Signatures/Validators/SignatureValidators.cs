using FluentValidation;
using FrotaOnline.Application.Signatures.Dtos;

namespace FrotaOnline.Application.Signatures.Validators;

public static class SignatureLimits
{
    public const int MaxSizeBytes = 2 * 1024 * 1024;

    public const int MaxBase64Length = MaxSizeBytes / 3 * 4 + 256;
}

public sealed class RegisterSignatureRequestValidator : AbstractValidator<RegisterSignatureRequest>
{
    public RegisterSignatureRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum().WithMessage("O tipo da assinatura deve ser Customer ou Driver.");
        RuleFor(x => x.FreightId).NotEmpty().WithMessage("Informe o frete.");
        RuleFor(x => x.Image).NotEmpty().WithMessage("Informe a imagem da assinatura em base64.")
            .MaximumLength(SignatureLimits.MaxBase64Length).WithMessage("A imagem da assinatura é maior que 2 MB.");
    }
}
