using FluentValidation;
using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Application.Marketplace.Dtos;

namespace FrotaOnline.Application.Marketplace.Validators;

public sealed class CreateFreightRequestValidator : AbstractValidator<CreateFreightRequest>
{
    public CreateFreightRequestValidator()
    {
        RuleFor(x => x.Stops).NotNull().WithMessage("Envie as paradas da rota.");
        When(x => x.Stops is not null, () =>
        {
            RuleFor(x => x.Stops)
                .Must(stops => stops.Count >= 2)
                .WithMessage("O frete precisa de ao menos uma coleta e uma entrega.")
                .Must(stops => stops.Count <= FreightLimits.MaxStops)
                .WithMessage($"O frete pode ter no máximo {FreightLimits.MaxStops} paradas.")
                .Must(stops => stops.Count == 0 || stops[0]?.Type == StopType.Pickup)
                .WithMessage("A primeira parada deve ser uma coleta.")
                .Must(stops => stops.Count == 0 || stops[^1]?.Type == StopType.Delivery)
                .WithMessage("A última parada deve ser uma entrega: é o endereço final de entrega.")
                .Must(stops => stops.Where(s => s is not null).Select(s => s.AddressId).Distinct().Count() == stops.Count(s => s is not null))
                .WithMessage("Cada parada deve usar um endereço diferente.");
            RuleForEach(x => x.Stops).NotNull().SetValidator(new CreateFreightStopRequestValidator());
        });
        RuleFor(x => x.CargoDescription).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.CargoWeightKg).GreaterThan(0).LessThanOrEqualTo(60_000);
        RuleFor(x => x.RequiredVehicleType).IsInEnum();
        RuleFor(x => x.DriverNote).MaximumLength(FreightLimits.MaxDriverNoteLength)
            .WithMessage($"A observação para o motorista pode ter no máximo {FreightLimits.MaxDriverNoteLength} caracteres.");
    }
}

public sealed class CreateFreightStopRequestValidator : AbstractValidator<CreateFreightStopRequest>
{
    public CreateFreightStopRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.AddressId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.Items)
            .Must(items => items is null || items.Count <= FreightLimits.MaxItemsPerStop)
            .WithMessage($"Uma parada pode ter no máximo {FreightLimits.MaxItemsPerStop} itens.");
        RuleForEach(x => x.Items).NotNull().SetValidator(new CreateFreightItemRequestValidator());
    }
}

public sealed class CreateFreightItemRequestValidator : AbstractValidator<CreateFreightItemRequest>
{
    public CreateFreightItemRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Quantity).InclusiveBetween(1, 100_000);
    }
}

public sealed class SubmitProposalRequestValidator : AbstractValidator<SubmitProposalRequest>
{
    public SubmitProposalRequestValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.Price).GreaterThan(0).LessThanOrEqualTo(1_000_000)
            .PrecisionScale(10, 2, false).WithMessage("O preço pode ter no máximo duas casas decimais.");
        RuleFor(x => x.Message).MaximumLength(500);
    }
}

public sealed class UploadCargoPhotoRequestValidator : AbstractValidator<UploadCargoPhotoRequest>
{
    public UploadCargoPhotoRequestValidator()
    {
        RuleFor(x => x.Image).NotEmpty().WithMessage("Informe a foto da carga em base64.")
            .MaximumLength(CargoPhotoLimits.MaxBase64Length).WithMessage("A foto da carga é maior que 2 MB.");
    }
}
