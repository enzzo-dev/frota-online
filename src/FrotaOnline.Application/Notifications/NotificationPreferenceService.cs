using FrotaOnline.Application.Abstractions.Notifications;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Notifications.Dtos;
using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Notifications;

public sealed class NotificationPreferenceService : INotificationPreferenceService
{
    private readonly INotificationPreferenceRepository _preferences;
    private readonly IUnitOfWork _unitOfWork;

    public NotificationPreferenceService(INotificationPreferenceRepository preferences, IUnitOfWork unitOfWork)
    {
        _preferences = preferences;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<NotificationPreferencesResponse>> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var preference = await _preferences.GetByUserIdAsync(userId, cancellationToken);
        return Result.Success(ToResponse(preference));
    }

    public async Task<Result<NotificationPreferencesResponse>> UpdateAsync(
        Guid userId, UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken)
    {
        var preference = await _preferences.GetByUserIdAsync(userId, cancellationToken);
        if (preference is null)
        {
            preference = NotificationPreference.Create(userId);
            await _preferences.AddAsync(preference, cancellationToken);
        }

        preference.Update(request.NewFreightAlerts, request.PushEnabled);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(preference));
    }

    private static NotificationPreferencesResponse ToResponse(NotificationPreference? preference) =>
        new(preference?.NewFreightAlerts ?? true, preference?.PushEnabled ?? true);
}
