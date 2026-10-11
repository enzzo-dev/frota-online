using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Domain.Entities;

public sealed class DeviceToken : BaseEntity
{
    private DeviceToken()
    {
    }

    public Guid UserId { get; private set; }

    public string Token { get; private set; } = string.Empty;

    public DevicePlatform Platform { get; private set; }

    public DateTime LastSeenAt { get; private set; }

    public static DeviceToken Create(Guid userId, string token, DevicePlatform platform, DateTime utcNow) => new()
    {
        UserId = Guard.NotEmpty(userId, nameof(userId)),
        Token = Guard.NotNullOrWhiteSpace(token, nameof(token)),
        Platform = platform,
        LastSeenAt = utcNow
    };

    public void MarkSeen(DateTime utcNow) => LastSeenAt = utcNow;

    /// <summary>The same phone signed in as another person (or a refreshed token): follow the latest login.</summary>
    public void Reassign(Guid userId, DevicePlatform platform, DateTime utcNow)
    {
        UserId = Guard.NotEmpty(userId, nameof(userId));
        Platform = platform;
        LastSeenAt = utcNow;
    }
}
