using FluentValidation;
using FrotaOnline.Application.Signatures.Dtos;

namespace FrotaOnline.Application.Signatures.Validators;

public static class SignatureLimits
{
    public const long MaxSizeBytes = 2 * 1024 * 1024;

    public static readonly string[] AllowedContentTypes = { "image/png", "image/jpeg" };
}

public sealed class CreateSignatureUploadUrlRequestValidator : AbstractValidator<CreateSignatureUploadUrlRequest>
{
    public CreateSignatureUploadUrlRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum().WithMessage("O tipo da assinatura deve ser Customer ou Driver.");
        RuleFor(x => x.FreightId).NotEmpty().WithMessage("Informe o frete.");
        RuleFor(x => x.ContentType).NotEmpty().Must(type => SignatureLimits.AllowedContentTypes.Contains(type))
            .WithMessage("O tipo do conteúdo deve ser um de: " + string.Join(", ", SignatureLimits.AllowedContentTypes) + ".");
    }
}

public sealed class RegisterSignatureRequestValidator : AbstractValidator<RegisterSignatureRequest>
{
    public RegisterSignatureRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum().WithMessage("O tipo da assinatura deve ser Customer ou Driver.");
        RuleFor(x => x.FreightId).NotEmpty().WithMessage("Informe o frete.");
        RuleFor(x => x.StoragePath).NotEmpty().MaximumLength(512);
    }
}
