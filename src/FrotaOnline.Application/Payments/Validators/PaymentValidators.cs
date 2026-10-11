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

public sealed class PayWithCardRequestValidator : AbstractValidator<PayWithCardRequest>
{
    public PayWithCardRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.SavedCardId.HasValue ^ x.Card is not null)
            .WithName("card")
            .WithMessage("Send either a saved card or the data of a new card.");

        When(x => x.Card is not null, () =>
        {
            RuleFor(x => x.Card!.HolderName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Card!.Number)
                .Must(value => CardNumbers.IsValid(value))
                .WithMessage("Enter a valid card number.");
            RuleFor(x => x.Card!.ExpiryMonth)
                .Must(value => int.TryParse(value, out var month) && month is >= 1 and <= 12)
                .WithMessage("Enter the expiry month (01 to 12).");
            RuleFor(x => x.Card!.ExpiryYear)
                .Must(value => value is { Length: 4 } && int.TryParse(value, out var year) && year >= 2000)
                .WithMessage("Enter the expiry year with four digits.");
            RuleFor(x => x.Card!.Ccv)
                .Must(value => value is { Length: 3 or 4 } && value.All(char.IsAsciiDigit))
                .WithMessage("Enter the security code (3 or 4 digits).");

            RuleFor(x => x.Holder).NotNull().WithMessage("The card holder data is required for a new card.");
            When(x => x.Holder is not null, () =>
            {
                RuleFor(x => x.Holder!.TaxId).Must(BrazilianDocuments.IsValidTaxId).WithMessage("Enter a valid CPF or CNPJ.");
                RuleFor(x => x.Holder!.PostalCode)
                    .Must(value => value is not null && value.Count(char.IsAsciiDigit) == 8 && value.All(c => char.IsAsciiDigit(c) || c == '-'))
                    .WithMessage("Enter the postal code (CEP) with 8 digits.");
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
