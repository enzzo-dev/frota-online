using FluentValidation;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Payments.Dtos;

namespace FrotaOnline.Application.Payments.Validators;

public sealed class SubscribeRequestValidator : AbstractValidator<SubscribeRequest>
{
    public SubscribeRequestValidator() => RuleFor(x => x.Plan).IsInEnum();
}

public sealed class UpdatePayoutAccountRequestValidator : AbstractValidator<UpdatePayoutAccountRequest>
{
    public UpdatePayoutAccountRequestValidator() =>
        RuleFor(x => x.WalletId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("The payout account id must be the wallet id of your payment account (a UUID).");
}

public sealed class CreatePixRequestValidator : AbstractValidator<CreatePixRequest>
{
    public CreatePixRequestValidator() =>
        RuleFor(x => x.TaxId)
            .NotEmpty()
            .Must(BrazilianDocuments.IsValidTaxId)
            .WithMessage("Enter a valid CPF or CNPJ.");
}
