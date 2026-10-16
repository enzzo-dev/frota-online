using System.ComponentModel.DataAnnotations;

namespace FrotaOnline.Infrastructure.Notifications;

public sealed class MailerSendOptions
{
    public const string SectionName = "Email";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Sender address. It must belong to a domain verified in MailerSend.</summary>
    [Required, EmailAddress]
    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Frota Online";
}
