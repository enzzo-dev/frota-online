using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Deliveries;

public static class DeliveryErrors
{
    public static Error InvalidStatus(string reason) =>
        new("delivery.invalid_status", reason, ErrorType.Conflict);

    public static readonly Error StopNotFound =
        new("delivery.stop_not_found", "Esta parada não pertence à entrega.", ErrorType.NotFound);

    public static Error StopOutOfOrder(int nextSequence) =>
        new("delivery.stop_out_of_order", $"Conclua as paradas em ordem. A próxima é a parada {nextSequence}.", ErrorType.Conflict);

    public static Error SignaturesMissing(bool customerMissing, bool driverMissing)
    {
        var who = customerMissing && driverMissing ? "do cliente e do motorista" : customerMissing ? "do cliente" : "do motorista";
        return new("delivery.signatures_missing", $"Colete a assinatura {who} antes de concluir a entrega.", ErrorType.Conflict);
    }

    public static readonly Error InvalidProof =
        new("delivery.invalid_proof", "O comprovante deve ser um arquivo DeliveryProof registrado por você.", ErrorType.Validation);

    public static readonly Error PaymentNotDelivered =
        new("payment.not_delivered", "O pagamento só pode ser confirmado depois que a entrega for concluída.", ErrorType.Conflict);

    public static readonly Error PaymentAlreadyConfirmed =
        new("payment.already_confirmed", "O pagamento desta entrega já foi confirmado.", ErrorType.Conflict);

    public static Error FeeExceedsBalance(decimal balance) =>
        new("fee.exceeds_balance", $"O valor é maior que o saldo em aberto (R$ {balance:0.00}).", ErrorType.Conflict);
}
