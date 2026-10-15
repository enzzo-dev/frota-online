using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Privacy;

public static class PrivacyErrors
{
    public static readonly Error AccountNotFound =
        new("privacy.account_not_found", "Conta não encontrada.", ErrorType.NotFound);

    public static readonly Error AdminNotAllowed =
        new("privacy.admin_not_allowed", "Contas de administrador devem ser removidas por outro administrador.", ErrorType.Forbidden);

    public static readonly Error ActiveDeliveries =
        new("privacy.active_deliveries", "Conclua ou cancele suas entregas em andamento antes de excluir a conta.", ErrorType.Conflict);
}
