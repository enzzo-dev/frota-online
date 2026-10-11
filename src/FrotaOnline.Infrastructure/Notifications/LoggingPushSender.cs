using FrotaOnline.Application.Abstractions.Notifications;
using Microsoft.Extensions.Logging;

namespace FrotaOnline.Infrastructure.Notifications;

/// <summary>
/// Placeholder sender: writes the push to the log and reports it as delivered. Replace it with a Firebase
/// Cloud Messaging (or APNs/OneSignal) implementation of <see cref="IPushSender"/> when the provider is chosen;
/// callers and the dispatcher do not change.
/// </summary>
internal sealed class LoggingPushSender : IPushSender
{
    private readonly ILogger<LoggingPushSender> _logger;

    public LoggingPushSender(ILogger<LoggingPushSender> logger) => _logger = logger;

    public Task<PushResult> SendAsync(PushMessage message, IReadOnlyList<PushDevice> devices, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Push to {Devices} device(s) | {Title} | {Body} | {Target}",
            devices.Count, message.Title, message.Body, message.Data.GetValueOrDefault("targetId"));
        return Task.FromResult(new PushResult(true, Array.Empty<string>()));
    }
}
