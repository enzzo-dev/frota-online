using FrotaOnline.Application.Notifications.Dtos;

namespace FrotaOnline.Application.Abstractions.Queries;

public interface INotificationQueries
{
    /// <summary>Newest first. The page is one SELECT with OFFSET/LIMIT and the unread total one COUNT.</summary>
    Task<NotificationListResponse> ListAsync(
        Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken);
}
