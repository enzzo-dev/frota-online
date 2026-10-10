using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Deliveries;

public static class DeliveryErrors
{
    public static Error InvalidStatus(string reason) =>
        new("delivery.invalid_status", reason, ErrorType.Conflict);

    public static readonly Error StopNotFound =
        new("delivery.stop_not_found", "This stop does not belong to the delivery.", ErrorType.NotFound);

    public static Error StopOutOfOrder(int nextSequence) =>
        new("delivery.stop_out_of_order", $"Complete the stops in order. The next one is stop {nextSequence}.", ErrorType.Conflict);

    public static readonly Error InvalidProof =
        new("delivery.invalid_proof", "The proof must be a DeliveryProof file registered by you.", ErrorType.Validation);

    public static readonly Error PaymentNotDelivered =
        new("payment.not_delivered", "Payment can only be confirmed after the delivery is completed.", ErrorType.Conflict);

    public static readonly Error PaymentAlreadyConfirmed =
        new("payment.already_confirmed", "The payment of this delivery was already confirmed.", ErrorType.Conflict);

    public static Error FeeExceedsBalance(decimal balance) =>
        new("fee.exceeds_balance", $"The amount is higher than the open balance (R$ {balance:0.00}).", ErrorType.Conflict);
}
