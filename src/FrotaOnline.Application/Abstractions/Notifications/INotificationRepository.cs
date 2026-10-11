using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Abstractions.Notifications;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Only stages the rows: they are saved with the business change that caused them.</summary>
    Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken);

    /// <summary>One UPDATE statement. Returns how many notifications were unread.</summary>
    Task<int> MarkAllReadAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken);

    /// <summary>
    /// Takes up to <paramref name="take"/> unread notifications whose push was not sent yet, oldest first, and
    /// leases them for a minute: another instance (or the next pass) will not get them in that time, so two
    /// instances never send the same push. A notification that failed, or whose holder crashed, comes back
    /// after the lease ends.
    /// </summary>
    Task<IReadOnlyList<Notification>> ClaimPendingPushAsync(DateTime utcNow, int take, CancellationToken cancellationToken);
}
