using FluentValidation;
using FrotaOnline.Application.Deliveries.Dtos;

namespace FrotaOnline.Application.Deliveries.Validators;

public sealed class CompleteStopRequestValidator : AbstractValidator<CompleteStopRequest>
{
    public CompleteStopRequestValidator()
    {
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
        RuleFor(x => x)
            .Must(x => x.Latitude.HasValue == x.Longitude.HasValue)
            .WithMessage("Envie latitude e longitude juntas.");
        RuleFor(x => x.ProofMediaFileId).NotEqual(Guid.Empty).When(x => x.ProofMediaFileId.HasValue);
    }
}

public sealed class UpdateLocationRequestValidator : AbstractValidator<UpdateLocationRequest>
{
    public UpdateLocationRequestValidator()
    {
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.Heading).InclusiveBetween(0, 360).When(x => x.Heading.HasValue);
        RuleFor(x => x.SpeedKmh).InclusiveBetween(0, 400).When(x => x.SpeedKmh.HasValue);
    }
}

public sealed class ReportIncidentRequestValidator : AbstractValidator<ReportIncidentRequest>
{
    public ReportIncidentRequestValidator() => RuleFor(x => x.Note).NotEmpty().MaximumLength(500);
}

public sealed class CancelDeliveryRequestValidator : AbstractValidator<CancelDeliveryRequest>
{
    public CancelDeliveryRequestValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
}

public sealed class ConfirmPaymentRequestValidator : AbstractValidator<ConfirmPaymentRequest>
{
    public ConfirmPaymentRequestValidator()
    {
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.Reference).MaximumLength(100);
    }
}

public sealed class SettleFeeRequestValidator : AbstractValidator<SettleFeeRequest>
{
    public SettleFeeRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(1_000_000)
            .PrecisionScale(10, 2, false).WithMessage("O valor pode ter no máximo duas casas decimais.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
    }
}
