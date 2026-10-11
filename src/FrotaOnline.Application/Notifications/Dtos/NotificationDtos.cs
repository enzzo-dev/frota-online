using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Notifications.Dtos;

public sealed record NotificationResponse(
    Guid Id,
    NotificationType Type,
    string Title,
    string Body,
    NotificationTarget TargetType,
    Guid TargetId,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt);

public sealed record NotificationListResponse(
    IReadOnlyList<NotificationResponse> Items, int UnreadCount, int Page, int PageSize, bool HasMore);

public sealed record UnreadCountResponse(int Unread);

public sealed record RegisterDeviceRequest(string Token, DevicePlatform Platform);

/// <summary>Both switches default to on. A person who never changed anything gets the defaults.</summary>
public sealed record NotificationPreferencesResponse(bool NewFreightAlerts, bool PushEnabled);

/// <summary>Send only what changes: a missing field keeps its current value.</summary>
public sealed record UpdateNotificationPreferencesRequest(bool? NewFreightAlerts, bool? PushEnabled);
