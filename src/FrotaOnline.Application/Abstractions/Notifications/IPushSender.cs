using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Abstractions.Notifications;

/// <summary>What the phone shows, plus the data the app uses to open the right screen.</summary>
public sealed record PushMessage(string Title, string Body, IReadOnlyDictionary<string, string> Data);

public sealed record PushDevice(string Token, DevicePlatform Platform);

/// <summary>
/// <paramref name="Delivered"/> is false for a problem worth retrying (provider down, timeout).
/// <paramref name="InvalidTokens"/> lists tokens the provider says no longer exist; they are deleted.
/// </summary>
public sealed record PushResult(bool Delivered, IReadOnlyList<string> InvalidTokens);

public interface IPushSender
{
    /// <summary>Sends one message to every device of one person. Implementations should not throw.</summary>
    Task<PushResult> SendAsync(PushMessage message, IReadOnlyList<PushDevice> devices, CancellationToken cancellationToken);
}
