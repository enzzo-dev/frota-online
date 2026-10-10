using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Privacy;

public static class PrivacyErrors
{
    public static readonly Error AccountNotFound =
        new("privacy.account_not_found", "Account not found.", ErrorType.NotFound);

    public static readonly Error AdminNotAllowed =
        new("privacy.admin_not_allowed", "Administrator accounts must be removed by another administrator.", ErrorType.Forbidden);

    public static readonly Error ActiveDeliveries =
        new("privacy.active_deliveries", "Finish or cancel your deliveries in progress before deleting the account.", ErrorType.Conflict);
}
