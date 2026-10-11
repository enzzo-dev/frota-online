using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Notifications;

/// <summary>A notification before it is stored: who, what, and where the app should take them.</summary>
public sealed record NotificationMessage(
    Guid UserId,
    NotificationType Type,
    string Title,
    string Body,
    NotificationTarget TargetType,
    Guid TargetId);
