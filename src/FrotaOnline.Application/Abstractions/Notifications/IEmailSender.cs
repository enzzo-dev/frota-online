namespace FrotaOnline.Application.Abstractions.Notifications;

public interface IEmailSender
{
    /// <summary>
    /// Sends an e-mail on a best-effort basis. Implementations must not throw: a delivery problem
    /// must never undo the business operation that triggered the message.
    /// </summary>
    Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken);
}
