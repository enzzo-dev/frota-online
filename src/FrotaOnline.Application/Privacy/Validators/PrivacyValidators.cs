using FluentValidation;
using FrotaOnline.Application.Privacy.Dtos;

namespace FrotaOnline.Application.Privacy.Validators;

public sealed class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator()
    {
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.Confirm).Equal(true).WithMessage("Envie confirm como true para excluir a conta.");
    }
}
