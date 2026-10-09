using FrotaOnline.Application.Abstractions.Notifications;
using Microsoft.Extensions.Logging;

namespace FrotaOnline.Infrastructure.Notifications;

/// <summary>
/// Placeholder sender: writes the message to the log. Replace it with an SMTP/SES/SendGrid
/// implementation of <see cref="IEmailSender"/> when a provider is chosen; callers do not change.
/// </summary>
internal sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken)
    {
        _logger.LogInformation("E-mail to {To} | {Subject} | {Body}", toAddress, subject, body);
        return Task.CompletedTask;
    }
}
