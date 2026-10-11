using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Payments;

public static class PaymentErrors
{
    /// <summary>Smallest charge the provider accepts.</summary>
    public const decimal MinimumCharge = 5.00m;

    public static readonly Error GatewayNotConfigured =
        new("payments.not_configured", "Online payments are not configured in this environment.", ErrorType.Conflict);

    public static readonly Error GatewayUnavailable =
        new("payments.unavailable", "The payment provider could not complete the request. Try again in a moment.", ErrorType.Unavailable);

    public static readonly Error DriverCannotReceive =
        new("payments.driver_without_payout_account",
            "The driver has not linked a payout account yet, so a Pix with split cannot be issued. Pay the driver directly and ask them to confirm.",
            ErrorType.Conflict);

    public static readonly Error PriceTooLow =
        new("payments.price_too_low", "The agreed price is not enough to cover the platform fee.", ErrorType.Conflict);

    public static readonly Error NotYourDelivery =
        new("payments.delivery_not_found", "Delivery not found.", ErrorType.NotFound);

    public static readonly Error NothingToCharge =
        new("fee.nothing_to_charge", "You have no platform fee to pay.", ErrorType.Conflict);

    public static Error FeeBelowMinimum(decimal balance) =>
        new("fee.below_minimum",
            $"The open balance (R$ {balance:0.00}) is below the minimum charge of R$ {MinimumCharge:0.00}. It is added to the next charges.",
            ErrorType.Conflict);
}

public static class SubscriptionErrors
{
    public static readonly Error NotFound =
        new("subscription.not_found", "You have no subscription.", ErrorType.NotFound);

    public static readonly Error AlreadyActive =
        new("subscription.already_active", "You already have an active subscription. Cancel it before choosing another plan.", ErrorType.Conflict);

    public static readonly Error Required =
        new("subscription.required", "An active subscription is needed to send proposals.", ErrorType.Forbidden);

    public static Error TooManyVehicles(int max) =>
        new("subscription.too_many_vehicles",
            $"The basic plan covers up to {max} vehicles. Deactivate some vehicles or choose the full plan.", ErrorType.Conflict);

    public static Error VehicleLimitReached(int max) =>
        new("subscription.vehicle_limit", $"Your plan allows up to {max} active vehicles. Choose the full plan to add more.", ErrorType.Conflict);
}
