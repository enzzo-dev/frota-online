using FluentValidation;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Drivers.Dtos;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Drivers.Validators;

public sealed class UpsertDriverProfileRequestValidator : AbstractValidator<UpsertDriverProfileRequest>
{
    private static readonly string[] Categories = { "A", "B", "C", "D", "E", "AB", "AC", "AD", "AE" };

    public UpsertDriverProfileRequestValidator()
    {
        RuleFor(x => x.TaxId).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("CPF ou CNPJ é obrigatório para motoristas.")
            .Must(BrazilianDocuments.IsValidTaxId)
            .WithMessage("CPF ou CNPJ inválido. Confira o número e os dígitos verificadores.");
        RuleFor(x => x.CnhNumber).NotEmpty()
            .Must(value => BrazilianDocuments.OnlyDigits(value).Length == 11)
            .WithMessage("A CNH deve ter 11 dígitos.");
        RuleFor(x => x.CnhCategory).NotEmpty()
            .Must(value => Categories.Contains((value ?? string.Empty).Trim().ToUpperInvariant()))
            .WithMessage("A categoria da CNH deve ser uma de: " + string.Join(", ", Categories) + ".");
    }
}

public sealed class RegisterVehicleRequestValidator : AbstractValidator<RegisterVehicleRequest>
{
    public RegisterVehicleRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Plate).NotEmpty().Must(BrazilianDocuments.IsValidPlate)
            .WithMessage("A placa deve seguir o formato ABC1234 ou ABC1D23.");
        RuleFor(x => x.Model).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Year).InclusiveBetween(1980, DateTime.UtcNow.Year + 1);
        RuleFor(x => x.CapacityKg).GreaterThan(0).LessThanOrEqualTo(60_000);
    }
}

public sealed class CreateUploadUrlRequestValidator : AbstractValidator<CreateUploadUrlRequest>
{
    public CreateUploadUrlRequestValidator()
    {
        RuleFor(x => x.Kind).Must(kind => kind is MediaKind.DriverDocument or MediaKind.VehiclePhoto or MediaKind.DeliveryProof)
            .WithMessage("Aqui o motorista só pode enviar arquivos do tipo DriverDocument, VehiclePhoto ou DeliveryProof.");
        RuleFor(x => x.ContentType).NotEmpty().Must(type => RegisterMediaRequestValidator.AllowedContentTypes.Contains(type))
            .WithMessage("O tipo do conteúdo deve ser um de: " + string.Join(", ", RegisterMediaRequestValidator.AllowedContentTypes) + ".");
    }
}

public sealed class RegisterMediaRequestValidator : AbstractValidator<RegisterMediaRequest>
{
    public const long MaxSizeBytes = 10 * 1024 * 1024;

    public static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png", "application/pdf" };

    public RegisterMediaRequestValidator()
    {
        RuleFor(x => x.Kind).Must(kind => kind is MediaKind.DriverDocument or MediaKind.VehiclePhoto or MediaKind.DeliveryProof)
            .WithMessage("Aqui o motorista só pode registrar arquivos do tipo DriverDocument, VehiclePhoto ou DeliveryProof.");
        RuleFor(x => x.StorageKey).NotEmpty().MaximumLength(512);
        RuleFor(x => x.ContentType).NotEmpty().Must(type => AllowedContentTypes.Contains(type))
            .WithMessage("O tipo do conteúdo deve ser um de: " + string.Join(", ", AllowedContentTypes) + ".");
        RuleFor(x => x.SizeBytes).GreaterThan(0).LessThanOrEqualTo(MaxSizeBytes);
    }
}
