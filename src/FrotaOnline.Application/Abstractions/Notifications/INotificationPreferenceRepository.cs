using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Abstractions.Notifications;

public interface INotificationPreferenceRepository
{
    Task<NotificationPreference?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    Task AddAsync(NotificationPreference preference, CancellationToken cancellationToken);

    /// <summary>Of the given people, the ones who turned pushes off. One query for the whole dispatch batch.</summary>
    Task<IReadOnlySet<Guid>> ListPushDisabledAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
