using FluentValidation;
using FrotaOnline.Application.Auth.Dtos;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Auth.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.PhoneNumber).NotEmpty().Matches(@"^\+?[0-9]{10,15}$")
            .WithMessage("O telefone deve ter de 10 a 15 dígitos, podendo começar com '+'.");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128)
            .Matches("[A-Z]").WithMessage("A senha deve conter uma letra maiúscula.")
            .Matches("[a-z]").WithMessage("A senha deve conter uma letra minúscula.")
            .Matches("[0-9]").WithMessage("A senha deve conter um número.");
        RuleFor(x => x.Role).Must(role => role is UserRole.Customer or UserRole.Driver)
            .WithMessage("Only Customer and Driver accounts can be self-registered.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
