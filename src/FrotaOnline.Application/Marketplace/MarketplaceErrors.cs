using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Marketplace;

public static class MarketplaceErrors
{
    public static readonly Error DeliveryNotFound =
        new("delivery.not_found", "Delivery not found.", ErrorType.NotFound);

    public static readonly Error FreightNotFound =
        new("freight.not_found", "Freight request not found.", ErrorType.NotFound);

    public static readonly Error FreightNotOpen =
        new("freight.not_open", "This freight is no longer open.", ErrorType.Conflict);

    public static readonly Error WindowClosed =
        new("freight.window_closed", "The proposal window for this freight has ended.", ErrorType.Conflict);

    public static readonly Error TermsNotAccepted =
        new("freight.terms_not_accepted", "Accept the current terms before requesting a freight.", ErrorType.Forbidden);

    public static readonly Error ProposalNotFound =
        new("proposal.not_found", "Proposal not found.", ErrorType.NotFound);

    public static readonly Error ProposalNotPending =
        new("proposal.not_pending", "This proposal is no longer pending.", ErrorType.Conflict);

    public static readonly Error AlreadyProposed =
        new("proposal.already_submitted", "You already have a pending proposal for this freight.", ErrorType.Conflict);

    public static readonly Error DriverNotApproved =
        new("proposal.driver_not_approved", "Only approved drivers can use the marketplace.", ErrorType.Forbidden);

    public static readonly Error DriverUnavailable =
        new("proposal.driver_unavailable", "The driver can no longer take this freight. Choose another proposal.", ErrorType.Conflict);

    public static readonly Error VehicleUnavailable =
        new("proposal.vehicle_unavailable", "The vehicle of this proposal is no longer active.", ErrorType.Conflict);

    public static readonly Error DriverBusy =
        new("proposal.driver_busy", "The driver already has a delivery in this period.", ErrorType.Conflict);

    public static Error VehicleNotEligible(string reason) =>
        new("proposal.vehicle_not_eligible", reason, ErrorType.Validation);

    public static Error ScheduledInPast =>
        ValidationMapper.ToError("ScheduledPickupAt", "The scheduled pickup must be in the future.");
}
