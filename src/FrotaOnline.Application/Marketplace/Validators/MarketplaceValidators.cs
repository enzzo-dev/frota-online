using FluentValidation;
using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Application.Marketplace.Dtos;

namespace FrotaOnline.Application.Marketplace.Validators;

public sealed class CreateFreightRequestValidator : AbstractValidator<CreateFreightRequest>
{
    public CreateFreightRequestValidator()
    {
        RuleFor(x => x.Stops).NotNull().WithMessage("Send the stops of the route.");
        When(x => x.Stops is not null, () =>
        {
            RuleFor(x => x.Stops)
                .Must(stops => stops.Count >= 2)
                .WithMessage("A freight needs at least one pickup and one delivery.")
                .Must(stops => stops.Count <= FreightLimits.MaxStops)
                .WithMessage($"A freight can have at most {FreightLimits.MaxStops} stops.")
                .Must(stops => stops.Count == 0 || stops[0]?.Type == StopType.Pickup)
                .WithMessage("The first stop must be a pickup.")
                .Must(stops => stops.Count == 0 || stops[^1]?.Type == StopType.Delivery)
                .WithMessage("The last stop must be a delivery: it is the final delivery address.")
                .Must(stops => stops.Where(s => s is not null).Select(s => s.AddressId).Distinct().Count() == stops.Count(s => s is not null))
                .WithMessage("Every stop must use a different address.");
            RuleForEach(x => x.Stops).NotNull().SetValidator(new CreateFreightStopRequestValidator());
        });
        RuleFor(x => x.CargoDescription).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.CargoWeightKg).GreaterThan(0).LessThanOrEqualTo(60_000);
        RuleFor(x => x.RequiredVehicleType).IsInEnum();
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
            .WithMessage($"A stop can have at most {FreightLimits.MaxItemsPerStop} items.");
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
            .PrecisionScale(10, 2, false).WithMessage("Price can have at most two decimal places.");
        RuleFor(x => x.Message).MaximumLength(500);
    }
}
