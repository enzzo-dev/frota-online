using FrotaOnline.Domain.Common;

namespace FrotaOnline.Domain.Entities;

/// <summary>
/// What a person wants to receive. There is at most one row per user; no row means the defaults (everything on),
/// so nobody has to be migrated when a new option appears.
/// </summary>
public sealed class NotificationPreference : BaseEntity
{
    private NotificationPreference()
    {
    }

    public Guid UserId { get; private set; }

    /// <summary>Drivers: be told when a new freight that fits one of their vehicles opens.</summary>
    public bool NewFreightAlerts { get; private set; } = true;

    /// <summary>Master switch for pushes. Off: the inbox still fills, the phone stays quiet.</summary>
    public bool PushEnabled { get; private set; } = true;

    public static NotificationPreference Create(Guid userId) => new()
    {
        UserId = Guard.NotEmpty(userId, nameof(userId))
    };

    /// <summary>Null keeps the current value, so the app can change one switch at a time.</summary>
    public void Update(bool? newFreightAlerts, bool? pushEnabled)
    {
        NewFreightAlerts = newFreightAlerts ?? NewFreightAlerts;
        PushEnabled = pushEnabled ?? PushEnabled;
    }
}
