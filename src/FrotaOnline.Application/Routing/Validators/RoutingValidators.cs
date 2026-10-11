using FluentValidation;
using FrotaOnline.Application.Routing.Dtos;
using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Routing.Validators;

public sealed class RoutePreviewRequestValidator : AbstractValidator<RoutePreviewRequest>
{
    public RoutePreviewRequestValidator()
    {
        RuleFor(x => x.AddressIds).NotNull().WithMessage("Send the addresses of the route.");
        When(x => x.AddressIds is not null, () =>
        {
            RuleFor(x => x.AddressIds)
                .Must(ids => ids.Count >= 2).WithMessage("A route needs at least two addresses.")
                .Must(ids => ids.Count <= FreightLimits.MaxStops)
                .WithMessage($"A route can have at most {FreightLimits.MaxStops} addresses.")
                .Must(ids => ids.All(id => id != Guid.Empty)).WithMessage("Every address id must be filled.")
                .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Every stop must use a different address.");
        });
    }
}
