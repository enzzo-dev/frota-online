using System.Net.Http.Headers;
using System.Net.Http.Json;
using FrotaOnline.Application.Abstractions.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FrotaOnline.Infrastructure.Notifications;

/// <summary>Sends plain-text e-mail through the MailerSend HTTP API. Failures are logged and never thrown.</summary>
internal sealed class MailerSendEmailSender : IEmailSender
{
    public const string Endpoint = "https://api.mailersend.com/v1/email";

    private readonly HttpClient _httpClient;
    private readonly MailerSendOptions _options;
    private readonly ILogger<MailerSendEmailSender> _logger;

    public MailerSendEmailSender(HttpClient httpClient, IOptions<MailerSendOptions> options, ILogger<MailerSendEmailSender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = JsonContent.Create(new
                {
                    from = new { email = _options.FromAddress, name = _options.FromName },
                    to = new[] { new { email = toAddress } },
                    subject,
                    text = body
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "MailerSend rejected the e-mail to {To}: {Status} {Response}",
                    toAddress, (int)response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Could not send the e-mail to {To}", toAddress);
        }
    }
}
