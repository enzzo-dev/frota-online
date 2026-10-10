using FluentValidation;
using FrotaOnline.Application.Marketplace.Dtos;

namespace FrotaOnline.Application.Marketplace.Validators;

public sealed class CreateFreightRequestValidator : AbstractValidator<CreateFreightRequest>
{
    public CreateFreightRequestValidator()
    {
        RuleFor(x => x.PickupAddressId).NotEmpty();
        RuleFor(x => x.DeliveryAddressId).NotEmpty()
            .NotEqual(x => x.PickupAddressId).WithMessage("Pickup and delivery addresses must be different.");
        RuleFor(x => x.CargoDescription).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.CargoWeightKg).GreaterThan(0).LessThanOrEqualTo(60_000);
        RuleFor(x => x.RequiredVehicleType).IsInEnum();
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
