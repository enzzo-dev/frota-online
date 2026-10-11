using FrotaOnline.Application.Abstractions.Notifications;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Notifications.Dtos;

namespace FrotaOnline.Application.Notifications;

public sealed class NotificationService : INotificationService
{
    private const int MaxPageSize = 100;

    private readonly INotificationRepository _notifications;
    private readonly INotificationQueries _queries;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public NotificationService(
        INotificationRepository notifications, INotificationQueries queries, IUnitOfWork unitOfWork, IDateTimeProvider clock)
    {
        _notifications = notifications;
        _queries = queries;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<NotificationListResponse>> ListAsync(
        Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken)
    {
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, MaxPageSize);
        return Result.Success(await _queries.ListAsync(userId, unreadOnly, safePage, safeSize, cancellationToken));
    }

    public async Task<Result<UnreadCountResponse>> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        Result.Success(new UnreadCountResponse(await _queries.CountUnreadAsync(userId, cancellationToken)));

    public async Task<Result> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = await _notifications.GetByIdAsync(notificationId, cancellationToken);
        if (notification is null || notification.UserId != userId)
        {
            return Result.Failure(NotificationErrors.NotFound);
        }

        notification.MarkRead(_clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        await _notifications.MarkAllReadAsync(userId, _clock.UtcNow, cancellationToken);
        return Result.Success();
    }
}
