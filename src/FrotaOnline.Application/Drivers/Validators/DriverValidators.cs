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
            .NotEmpty().WithMessage("CPF or CNPJ is required for drivers.")
            .Must(BrazilianDocuments.IsValidTaxId)
            .WithMessage("CPF or CNPJ is invalid. Check the number and its verification digits.");
        RuleFor(x => x.CnhNumber).NotEmpty()
            .Must(value => BrazilianDocuments.OnlyDigits(value).Length == 11)
            .WithMessage("CNH must have 11 digits.");
        RuleFor(x => x.CnhCategory).NotEmpty()
            .Must(value => Categories.Contains((value ?? string.Empty).Trim().ToUpperInvariant()))
            .WithMessage("CNH category must be one of: " + string.Join(", ", Categories) + ".");
    }
}

public sealed class RegisterVehicleRequestValidator : AbstractValidator<RegisterVehicleRequest>
{
    public RegisterVehicleRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Plate).NotEmpty().Must(BrazilianDocuments.IsValidPlate)
            .WithMessage("Plate must follow the ABC1234 or ABC1D23 format.");
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
            .WithMessage("Drivers can only upload DriverDocument, VehiclePhoto or DeliveryProof files here.");
        RuleFor(x => x.ContentType).NotEmpty().Must(type => RegisterMediaRequestValidator.AllowedContentTypes.Contains(type))
            .WithMessage("Content type must be one of: " + string.Join(", ", RegisterMediaRequestValidator.AllowedContentTypes) + ".");
    }
}

public sealed class RegisterMediaRequestValidator : AbstractValidator<RegisterMediaRequest>
{
    public const long MaxSizeBytes = 10 * 1024 * 1024;

    public static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png", "application/pdf" };

    public RegisterMediaRequestValidator()
    {
        RuleFor(x => x.Kind).Must(kind => kind is MediaKind.DriverDocument or MediaKind.VehiclePhoto or MediaKind.DeliveryProof)
            .WithMessage("Drivers can only register DriverDocument, VehiclePhoto or DeliveryProof files here.");
        RuleFor(x => x.StorageKey).NotEmpty().MaximumLength(512);
        RuleFor(x => x.ContentType).NotEmpty().Must(type => AllowedContentTypes.Contains(type))
            .WithMessage("Content type must be one of: " + string.Join(", ", AllowedContentTypes) + ".");
        RuleFor(x => x.SizeBytes).GreaterThan(0).LessThanOrEqualTo(MaxSizeBytes);
    }
}
