using FrotaOnline.Application.Abstractions.Notifications;
using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Notifications;

public sealed class NotificationPublisher : INotificationPublisher
{
    private readonly INotificationRepository _notifications;

    public NotificationPublisher(INotificationRepository notifications) => _notifications = notifications;

    public Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken) =>
        PublishAsync(new[] { message }, cancellationToken);

    public Task PublishAsync(IReadOnlyCollection<NotificationMessage> messages, CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
        {
            return Task.CompletedTask;
        }

        var rows = messages.Select(m => Notification.Create(m.UserId, m.Type, m.Title, m.Body, m.TargetType, m.TargetId));
        return _notifications.AddRangeAsync(rows, cancellationToken);
    }
}
