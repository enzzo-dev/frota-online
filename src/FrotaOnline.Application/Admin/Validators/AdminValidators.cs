using FluentValidation;
using FrotaOnline.Application.Admin.Dtos;

namespace FrotaOnline.Application.Admin.Validators;

public sealed class RejectDriverRequestValidator : AbstractValidator<RejectDriverRequest>
{
    public RejectDriverRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
