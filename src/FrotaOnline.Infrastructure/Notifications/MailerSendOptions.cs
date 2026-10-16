using System.ComponentModel.DataAnnotations;

namespace FrotaOnline.Infrastructure.Notifications;

public sealed class MailerSendOptions
{
    public const string SectionName = "Email";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Frota Online";
}
