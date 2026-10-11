using FrotaOnline.Application.Abstractions.Notifications;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Time;

namespace FrotaOnline.Application.Notifications;

/// <summary>
/// Turns stored notifications into pushes. A person with no registered device, or who turned pushes off, is settled right
/// away (the inbox is enough); a provider failure is retried a minute later, up to <c>Notification.MaxPushAttempts</c> times.
/// </summary>
public sealed class NotificationDispatcher : INotificationDispatcher
{
    private const int BatchSize = 100;

    private readonly INotificationRepository _notifications;
    private readonly IDeviceTokenRepository _devices;
    private readonly INotificationPreferenceRepository _preferences;
    private readonly IPushSender _push;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public NotificationDispatcher(
        INotificationRepository notifications,
        IDeviceTokenRepository devices,
        INotificationPreferenceRepository preferences,
        IPushSender push,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock)
    {
        _notifications = notifications;
        _devices = devices;
        _preferences = preferences;
        _push = push;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var pending = await _notifications.ClaimPendingPushAsync(now, BatchSize, cancellationToken);
        if (pending.Count == 0)
        {
            return 0;
        }

        var userIds = pending.Select(n => n.UserId).Distinct().ToList();
        var tokens = (await _devices.ListByUserIdsAsync(userIds, cancellationToken))
            .ToLookup(t => t.UserId, t => new PushDevice(t.Token, t.Platform));

        var pushOff = await _preferences.ListPushDisabledAsync(userIds, cancellationToken);

        var invalid = new HashSet<string>();
        var settled = 0;

        foreach (var notification in pending)
        {
            var devices = tokens[notification.UserId].ToList();
            if (devices.Count == 0 || pushOff.Contains(notification.UserId))
            {
                notification.MarkPushDispatched(now);
                settled++;
                continue;
            }

            var message = new PushMessage(notification.Title, notification.Body, new Dictionary<string, string>
            {
                ["notificationId"] = notification.Id.ToString(),
                ["type"] = notification.Type.ToString(),
                ["targetType"] = notification.TargetType.ToString(),
                ["targetId"] = notification.TargetId.ToString()
            });

            PushResult result;
            try
            {
                result = await _push.SendAsync(message, devices, cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                result = new PushResult(false, Array.Empty<string>());
            }

            foreach (var token in result.InvalidTokens)
            {
                invalid.Add(token);
            }

            if (result.Delivered)
            {
                notification.MarkPushDispatched(now);
                settled++;
            }
            else
            {
                notification.RegisterPushFailure(now);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (invalid.Count > 0)
        {
            await _devices.RemoveByTokensAsync(invalid.ToList(), cancellationToken);
        }

        return settled;
    }
}
