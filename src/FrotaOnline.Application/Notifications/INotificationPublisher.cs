namespace FrotaOnline.Application.Notifications;

/// <summary>
/// Records notifications as part of the operation that caused them. It only stages the rows: the caller's own
/// <c>SaveChangesAsync</c> commits them together with the business change, so a notification never exists for
/// something that was rolled back, and a committed change never loses its notification. Push is sent later
/// by <see cref="INotificationDispatcher"/>.
/// </summary>
public interface INotificationPublisher
{
    Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken);

    Task PublishAsync(IReadOnlyCollection<NotificationMessage> messages, CancellationToken cancellationToken);
}
