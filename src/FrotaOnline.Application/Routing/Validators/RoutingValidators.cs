using FluentValidation;
using FrotaOnline.Application.Routing.Dtos;
using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Routing.Validators;

public sealed class RoutePreviewRequestValidator : AbstractValidator<RoutePreviewRequest>
{
    public RoutePreviewRequestValidator()
    {
        RuleFor(x => x.AddressIds).NotNull().WithMessage("Envie os endereços da rota.");
        When(x => x.AddressIds is not null, () =>
        {
            RuleFor(x => x.AddressIds)
                .Must(ids => ids.Count >= 2).WithMessage("A rota precisa de pelo menos dois endereços.")
                .Must(ids => ids.Count <= FreightLimits.MaxStops)
                .WithMessage($"A rota pode ter no máximo {FreightLimits.MaxStops} endereços.")
                .Must(ids => ids.All(id => id != Guid.Empty)).WithMessage("Todos os ids de endereço devem ser preenchidos.")
                .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Cada parada deve usar um endereço diferente.");
        });
    }
}
