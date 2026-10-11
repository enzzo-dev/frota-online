using FrotaOnline.Application.Common;
using FrotaOnline.Application.Notifications.Dtos;

namespace FrotaOnline.Application.Notifications;

public interface INotificationPreferenceService
{
    Task<Result<NotificationPreferencesResponse>> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<NotificationPreferencesResponse>> UpdateAsync(
        Guid userId, UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken);
}
