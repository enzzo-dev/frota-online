using FluentValidation;
using FrotaOnline.Application.Terms.Dtos;

namespace FrotaOnline.Application.Terms.Validators;

public sealed class AcceptTermRequestValidator : AbstractValidator<AcceptTermRequest>
{
    public AcceptTermRequestValidator()
    {
        RuleFor(x => x.TermDocumentId).NotEmpty();
    }
}

public sealed class PublishTermRequestValidator : AbstractValidator<PublishTermRequest>
{
    public PublishTermRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Version).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ContentUrl).NotEmpty().MaximumLength(1024)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            .WithMessage("Content URL must be an absolute https URL.");
    }
}
