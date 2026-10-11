using FrotaOnline.Application.Common;
using FrotaOnline.Application.Notifications.Dtos;

namespace FrotaOnline.Application.Notifications;

public interface INotificationService
{
    /// <summary>page starts at 1; pageSize is kept between 1 and 100.</summary>
    Task<Result<NotificationListResponse>> ListAsync(
        Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken);

    Task<Result<UnreadCountResponse>> CountUnreadAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken);

    Task<Result> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken);
}
