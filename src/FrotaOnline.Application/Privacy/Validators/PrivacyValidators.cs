using FluentValidation;
using FrotaOnline.Application.Privacy.Dtos;

namespace FrotaOnline.Application.Privacy.Validators;

public sealed class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator()
    {
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.Confirm).Equal(true).WithMessage("Set confirm to true to delete the account.");
    }
}
