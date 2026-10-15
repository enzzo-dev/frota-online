using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Marketplace;

public static class MarketplaceErrors
{
    public static readonly Error DeliveryNotFound =
        new("delivery.not_found", "Entrega não encontrada.", ErrorType.NotFound);

    public static readonly Error FreightNotFound =
        new("freight.not_found", "Frete não encontrado.", ErrorType.NotFound);

    public static readonly Error FreightNotOpen =
        new("freight.not_open", "Este frete não está mais aberto.", ErrorType.Conflict);

    public static readonly Error WindowClosed =
        new("freight.window_closed", "O prazo para propostas deste frete terminou.", ErrorType.Conflict);

    public static readonly Error TermsNotAccepted =
        new("freight.terms_not_accepted", "Aceite os termos atuais antes de solicitar um frete.", ErrorType.Forbidden);

    public static readonly Error ProposalNotFound =
        new("proposal.not_found", "Proposta não encontrada.", ErrorType.NotFound);

    public static readonly Error ProposalNotPending =
        new("proposal.not_pending", "Esta proposta não está mais pendente.", ErrorType.Conflict);

    public static readonly Error AlreadyProposed =
        new("proposal.already_submitted", "Você já tem uma proposta pendente para este frete.", ErrorType.Conflict);

    public static readonly Error DriverNotApproved =
        new("proposal.driver_not_approved", "Só motoristas aprovados podem usar o marketplace.", ErrorType.Forbidden);

    public static readonly Error DriverUnavailable =
        new("proposal.driver_unavailable", "O motorista não pode mais assumir este frete. Escolha outra proposta.", ErrorType.Conflict);

    public static readonly Error VehicleUnavailable =
        new("proposal.vehicle_unavailable", "O veículo desta proposta não está mais ativo.", ErrorType.Conflict);

    public static readonly Error DriverBusy =
        new("proposal.driver_busy", "O motorista já tem uma entrega neste período.", ErrorType.Conflict);

    public static Error VehicleNotEligible(string reason) =>
        new("proposal.vehicle_not_eligible", reason, ErrorType.Validation);

    public static Error ScheduledInPast =>
        ValidationMapper.ToError("ScheduledPickupAt", "A coleta agendada deve ser no futuro.");
}
