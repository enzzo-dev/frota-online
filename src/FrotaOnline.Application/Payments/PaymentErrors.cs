using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Payments;

public static class PaymentErrors
{
    /// <summary>Smallest charge the provider accepts.</summary>
    public const decimal MinimumCharge = 5.00m;

    public static readonly Error GatewayNotConfigured =
        new("payments.not_configured", "Os pagamentos online não estão configurados neste ambiente.", ErrorType.Conflict);

    public static readonly Error GatewayUnavailable =
        new("payments.unavailable", "O provedor de pagamento não conseguiu concluir a operação. Tente novamente em instantes.", ErrorType.Unavailable);

    public static readonly Error DriverCannotReceive =
        new("payments.driver_without_payout_account",
            "O motorista ainda não vinculou uma conta de recebimento, então não é possível gerar um Pix com divisão. Pague o motorista diretamente e peça que ele confirme.",
            ErrorType.Conflict);

    public static readonly Error DriverCannotReceiveWallet =
        new("payments.driver_without_wallet_account",
            "O motorista ainda não conectou a conta do Mercado Pago, então o pagamento por carteira digital não está disponível para este frete. Pague por Pix ou cartão.",
            ErrorType.Conflict);

    public static readonly Error PaymentInProgress =
        new("payments.in_progress",
            "Um pagamento por cartão ou carteira digital deste frete já está sendo confirmado. Aguarde o resultado antes de tentar novamente.",
            ErrorType.Conflict);

    public static readonly Error CardDeclined =
        new("payments.card_declined", "O cartão não foi aceito. Confira os dados ou tente outro cartão.", ErrorType.Conflict);

    public static readonly Error CardNotFound =
        new("payments.card_not_found", "Cartão salvo não encontrado.", ErrorType.NotFound);

    public static readonly Error WalletAuthorizationInvalid =
        new("wallet.authorization_invalid", "O link de autorização é inválido ou expirou. Inicie a conexão novamente.", ErrorType.Validation);

    public static readonly Error PriceTooLow =
        new("payments.price_too_low", "O valor combinado não é suficiente para cobrir a taxa da plataforma.", ErrorType.Conflict);

    public static readonly Error NotYourDelivery =
        new("payments.delivery_not_found", "Entrega não encontrada.", ErrorType.NotFound);

    public static readonly Error NothingToCharge =
        new("fee.nothing_to_charge", "Você não tem taxa da plataforma a pagar.", ErrorType.Conflict);

    public static Error FeeBelowMinimum(decimal balance) =>
        new("fee.below_minimum",
            $"O saldo em aberto (R$ {balance:0.00}) está abaixo da cobrança mínima de R$ {MinimumCharge:0.00}. Ele será somado às próximas cobranças.",
            ErrorType.Conflict);
}

public static class SubscriptionErrors
{
    public static readonly Error NotFound =
        new("subscription.not_found", "Você não tem assinatura.", ErrorType.NotFound);

    public static readonly Error AlreadyActive =
        new("subscription.already_active", "Você já tem uma assinatura ativa. Cancele-a antes de escolher outro plano.", ErrorType.Conflict);

    public static readonly Error Required =
        new("subscription.required",
            "Seu frete gratuito já foi usado. É preciso ter uma assinatura ativa para enviar mais propostas.", ErrorType.Forbidden);

    public static Error TooManyVehicles(int max) =>
        new("subscription.too_many_vehicles",
            $"O plano básico cobre até {max} veículos. Desative alguns veículos ou escolha o plano completo.", ErrorType.Conflict);

    public static Error VehicleLimitReached(int max) =>
        new("subscription.vehicle_limit", $"Seu plano permite até {max} veículos ativos. Escolha o plano completo para adicionar mais.", ErrorType.Conflict);
}
