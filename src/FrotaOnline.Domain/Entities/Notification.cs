using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Domain.Entities;

/// <summary>
/// One message for one person. The row is the in-app inbox entry and, until <see cref="PushDispatchedAt"/>
/// is set, also the queue the push worker reads.
/// </summary>
public sealed class Notification : BaseEntity
{
    public const int MaxPushAttempts = 5;

    private Notification()
    {
    }

    public Guid UserId { get; private set; }

    public NotificationType Type { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public NotificationTarget TargetType { get; private set; }

    public Guid TargetId { get; private set; }

    public DateTime? ReadAt { get; private set; }

    /// <summary>Null while the push is still to be sent (or retried).</summary>
    public DateTime? PushDispatchedAt { get; private set; }

    public int PushAttempts { get; private set; }

    public bool IsRead => ReadAt is not null;

    public static Notification Create(
        Guid userId, NotificationType type, string title, string body, NotificationTarget targetType, Guid targetId) => new()
    {
        UserId = Guard.NotEmpty(userId, nameof(userId)),
        Type = type,
        Title = Guard.NotNullOrWhiteSpace(title, nameof(title)),
        Body = Guard.NotNullOrWhiteSpace(body, nameof(body)),
        TargetType = targetType,
        TargetId = Guard.NotEmpty(targetId, nameof(targetId))
    };

    public void MarkRead(DateTime utcNow) => ReadAt ??= utcNow;

    public void MarkPushDispatched(DateTime utcNow) => PushDispatchedAt = utcNow;

    /// <summary>Records a failed send. After <see cref="MaxPushAttempts"/> the push is given up; the inbox entry stays.</summary>
    public void RegisterPushFailure(DateTime utcNow)
    {
        PushAttempts++;
        if (PushAttempts >= MaxPushAttempts)
        {
            PushDispatchedAt = utcNow;
        }
    }
}
