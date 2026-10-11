using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Notifications;

public static class NotificationErrors
{
    public static readonly Error NotFound =
        new("notification.not_found", "Notification not found.", ErrorType.NotFound);
}
