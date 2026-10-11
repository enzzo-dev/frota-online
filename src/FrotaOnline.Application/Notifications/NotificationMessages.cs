using System.Globalization;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Notifications;

/// <summary>
/// The text of every notification, in one place. The app is Brazilian, so the texts are in Portuguese.
/// Free text typed by someone else (incident note, cancel reason) is cut short so a push stays readable.
/// </summary>
public static class NotificationMessages
{
    private const int MaxQuote = 140;

    public static NotificationMessage ProposalReceived(Guid customerId, Guid freightId, decimal price) =>
        new(customerId, NotificationType.ProposalReceived, "Nova proposta recebida",
            $"Um motorista ofereceu {Money(price)} pelo seu frete. Veja as propostas e escolha a melhor.",
            NotificationTarget.Freight, freightId);

    /// <summary>Tells a driver a freight that fits one of their vehicles just opened.</summary>
    public static NotificationMessage NewFreightAvailable(
        Guid driverUserId, Guid freightId, string originCity, string destinationCity, bool sameDay) =>
        new(driverUserId, NotificationType.NewFreightAvailable, "Novo frete compatível",
            $"{Cut(originCity, 40)} → {Cut(destinationCity, 40)}. " +
            (sameDay
                ? "Para hoje: você tem 5 minutos para enviar sua proposta."
                : "Agendado: você tem até 24 horas para enviar sua proposta."),
            NotificationTarget.Freight, freightId);

    public static NotificationMessage ProposalApproved(Guid driverUserId, Guid deliveryId) =>
        new(driverUserId, NotificationType.ProposalApproved, "Proposta aprovada",
            "O cliente aprovou a sua proposta. A entrega foi agendada na sua agenda.",
            NotificationTarget.Delivery, deliveryId);

    public static NotificationMessage ProposalNotSelected(Guid driverUserId, Guid proposalId) =>
        new(driverUserId, NotificationType.ProposalNotSelected, "Proposta não selecionada",
            "O cliente escolheu outra proposta para este frete.",
            NotificationTarget.Proposal, proposalId);

    public static NotificationMessage ProposalExpired(Guid driverUserId, Guid proposalId) =>
        new(driverUserId, NotificationType.ProposalExpired, "Proposta expirada",
            "O prazo da sua proposta terminou sem resposta do cliente.",
            NotificationTarget.Proposal, proposalId);

    public static NotificationMessage FreightExpired(Guid customerId, Guid freightId) =>
        new(customerId, NotificationType.FreightExpired, "Frete expirado",
            "O prazo para receber propostas terminou sem que nenhuma fosse aprovada. Crie um novo frete se ainda precisar.",
            NotificationTarget.Freight, freightId);

    public static NotificationMessage DeliveryStarted(Guid customerId, Guid deliveryId) =>
        new(customerId, NotificationType.DeliveryStarted, "Motorista a caminho",
            "O motorista iniciou o deslocamento para a coleta.",
            NotificationTarget.Delivery, deliveryId);

    public static NotificationMessage DeliveryPickedUp(Guid customerId, Guid deliveryId) =>
        new(customerId, NotificationType.DeliveryPickedUp, "Carga coletada",
            "O motorista coletou a carga e está a caminho do destino.",
            NotificationTarget.Delivery, deliveryId);

    public static NotificationMessage DeliveryStopCompleted(Guid customerId, Guid deliveryId, int completed, int total) =>
        new(customerId, NotificationType.DeliveryStopCompleted, "Parada concluída",
            $"{completed} de {total} paradas concluídas.",
            NotificationTarget.Delivery, deliveryId);

    public static NotificationMessage DeliveryCompleted(Guid customerId, Guid deliveryId) =>
        new(customerId, NotificationType.DeliveryCompleted, "Entrega concluída",
            "Seu frete foi entregue. O pagamento é combinado diretamente com o motorista.",
            NotificationTarget.Delivery, deliveryId);

    public static NotificationMessage DeliveryIncident(Guid customerId, Guid deliveryId, string note) =>
        new(customerId, NotificationType.DeliveryIncident, "Ocorrência na entrega",
            $"O motorista registrou uma ocorrência: {Quote(note)}",
            NotificationTarget.Delivery, deliveryId);

    public static NotificationMessage DeliveryCancelledByDriver(Guid customerId, Guid deliveryId, string reason) =>
        new(customerId, NotificationType.DeliveryCancelled, "Entrega cancelada pelo motorista",
            $"Motivo: {Quote(reason)} Você pode criar um novo frete.",
            NotificationTarget.Delivery, deliveryId);

    public static NotificationMessage DeliveryCancelledByCustomer(Guid driverUserId, Guid deliveryId, string reason) =>
        new(driverUserId, NotificationType.DeliveryCancelled, "Entrega cancelada pelo cliente",
            $"Motivo: {Quote(reason)} Sua agenda foi liberada.",
            NotificationTarget.Delivery, deliveryId);

    public static NotificationMessage PaymentConfirmed(Guid customerId, Guid deliveryId, decimal amount) =>
        new(customerId, NotificationType.PaymentConfirmed, "Pagamento registrado",
            $"O motorista registrou o recebimento de {Money(amount)}.",
            NotificationTarget.Delivery, deliveryId);

    public static NotificationMessage DriverApproved(Guid driverUserId, Guid driverProfileId) =>
        new(driverUserId, NotificationType.DriverApproved, "Cadastro aprovado",
            "Seu cadastro como motorista foi aprovado. Você já pode enviar propostas de frete.",
            NotificationTarget.DriverProfile, driverProfileId);

    public static NotificationMessage DriverRejected(Guid driverUserId, Guid driverProfileId, string? reason) =>
        new(driverUserId, NotificationType.DriverRejected, "Cadastro precisa de ajustes",
            $"Não foi possível aprovar seu cadastro: {Quote(reason)} Corrija e envie novamente.",
            NotificationTarget.DriverProfile, driverProfileId);

    public static NotificationMessage FeeSettled(Guid driverUserId, Guid driverProfileId, decimal amount) =>
        new(driverUserId, NotificationType.FeeSettled, "Taxa quitada",
            $"Recebemos {Money(amount)} da sua taxa da plataforma.",
            NotificationTarget.Fee, driverProfileId);

    public static NotificationMessage PaymentReceived(Guid driverUserId, Guid deliveryId, decimal driverShare) =>
        new(driverUserId, NotificationType.PaymentReceived, "Pagamento recebido",
            $"O cliente pagou o frete pelo app. {Money(driverShare)} foram enviados para a sua conta de pagamentos.",
            NotificationTarget.Delivery, deliveryId);

    public static NotificationMessage SubscriptionActivated(Guid driverUserId, Guid subscriptionId, DateTime paidUntil) =>
        new(driverUserId, NotificationType.SubscriptionActivated, "Assinatura ativa",
            $"Pagamento confirmado. Seu plano está ativo até {paidUntil:dd/MM/yyyy}.",
            NotificationTarget.Subscription, subscriptionId);

    /// <summary>Brazilian format without depending on the server culture (containers often run without ICU).</summary>
    private static string Money(decimal value)
    {
        var text = value.ToString("N2", CultureInfo.InvariantCulture)
            .Replace(",", "_").Replace(".", ",").Replace("_", ".");
        return $"R$ {text}";
    }

    private static string Cut(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";

    private static string Quote(string? text)
    {
        var value = string.IsNullOrWhiteSpace(text) ? "sem detalhes." : text.Trim();
        return value.Length <= MaxQuote ? value : value[..(MaxQuote - 1)] + "…";
    }
}
