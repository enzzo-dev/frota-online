using FluentValidation;
using FrotaOnline.Application.Notifications.Dtos;

namespace FrotaOnline.Application.Notifications.Validators;

public sealed class RegisterDeviceRequestValidator : AbstractValidator<RegisterDeviceRequest>
{
    public RegisterDeviceRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(512);
        RuleFor(x => x.Platform).IsInEnum();
    }
}
