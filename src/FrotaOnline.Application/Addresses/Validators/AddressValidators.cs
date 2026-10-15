using FluentValidation;
using FrotaOnline.Application.Addresses.Dtos;
using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Addresses.Validators;

public sealed class CreateAddressRequestValidator : AbstractValidator<CreateAddressRequest>
{
    private static readonly string[] States =
    {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
        "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO"
    };

    public CreateAddressRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Street).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Number).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Complement).MaximumLength(100);
        RuleFor(x => x.District).NotEmpty().MaximumLength(100);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.State).NotEmpty()
            .Must(state => States.Contains((state ?? string.Empty).Trim().ToUpperInvariant()))
            .WithMessage("O estado deve ser uma sigla de UF válida com duas letras.");
        RuleFor(x => x.ZipCode).NotEmpty()
            .Must(zip => BrazilianDocuments.OnlyDigits(zip).Length == 8)
            .WithMessage("O CEP deve ter 8 dígitos.");
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
        RuleFor(x => x)
            .Must(x => x.Latitude.HasValue == x.Longitude.HasValue)
            .WithName("Latitude")
            .WithMessage("Envie latitude e longitude juntas.");
    }
}
