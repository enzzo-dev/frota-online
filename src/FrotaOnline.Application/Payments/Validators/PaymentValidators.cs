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
            .WithMessage("O id da conta de recebimento deve ser o id da carteira da sua conta de pagamento (um UUID).");
}

public sealed class CreatePixRequestValidator : AbstractValidator<CreatePixRequest>
{
    public CreatePixRequestValidator() =>
        RuleFor(x => x.TaxId)
            .NotEmpty()
            .Must(BrazilianDocuments.IsValidTaxId)
            .WithMessage("Informe um CPF ou CNPJ válido.");
}

public sealed class PayWithCardRequestValidator : AbstractValidator<PayWithCardRequest>
{
    public PayWithCardRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.SavedCardId.HasValue ^ x.Card is not null)
            .WithName("card")
            .WithMessage("Envie um cartão salvo ou os dados de um novo cartão.");

        When(x => x.Card is not null, () =>
        {
            RuleFor(x => x.Card!.HolderName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Card!.Number)
                .Must(value => CardNumbers.IsValid(value))
                .WithMessage("Informe um número de cartão válido.");
            RuleFor(x => x.Card!.ExpiryMonth)
                .Must(value => int.TryParse(value, out var month) && month is >= 1 and <= 12)
                .WithMessage("Informe o mês de validade (01 a 12).");
            RuleFor(x => x.Card!.ExpiryYear)
                .Must(value => value is { Length: 4 } && int.TryParse(value, out var year) && year >= 2000)
                .WithMessage("Informe o ano de validade com quatro dígitos.");
            RuleFor(x => x.Card!.Ccv)
                .Must(value => value is { Length: 3 or 4 } && value.All(char.IsAsciiDigit))
                .WithMessage("Informe o código de segurança (3 ou 4 dígitos).");

            RuleFor(x => x.Holder).NotNull().WithMessage("Os dados do titular são obrigatórios para um novo cartão.");
            When(x => x.Holder is not null, () =>
            {
                RuleFor(x => x.Holder!.TaxId).Must(BrazilianDocuments.IsValidTaxId).WithMessage("Informe um CPF ou CNPJ válido.");
                RuleFor(x => x.Holder!.PostalCode)
                    .Must(value => value is not null && value.Count(char.IsAsciiDigit) == 8 && value.All(c => char.IsAsciiDigit(c) || c == '-'))
                    .WithMessage("Informe o CEP com 8 dígitos.");
                RuleFor(x => x.Holder!.AddressNumber).NotEmpty().MaximumLength(20);
            });
        });
    }
}

public sealed class WalletPaymentRequestValidator : AbstractValidator<WalletPaymentRequest>
{
    public WalletPaymentRequestValidator()
    {
        RuleFor(x => x.CardToken).NotEmpty().MaximumLength(256);
        RuleFor(x => x.PaymentMethodId).NotEmpty().MaximumLength(32);
    }
}

/// <summary>Card number checks that need no provider: length and the Luhn check digit.</summary>
internal static class CardNumbers
{
    public static bool IsValid(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (digits.Length is < 13 or > 19)
        {
            return false;
        }

        var sum = 0;
        var alternate = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9)
                {
                    n -= 9;
                }
            }

            sum += n;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }
}
