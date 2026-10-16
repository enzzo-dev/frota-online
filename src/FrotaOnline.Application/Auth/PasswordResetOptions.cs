namespace FrotaOnline.Application.Auth;

public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    /// <summary>How long a reset token stays valid.</summary>
    public int TokenMinutes { get; set; } = 30;

    /// <summary>Front-end page that receives the token as "?token=...". When empty, the e-mail carries only the token.</summary>
    public string ResetUrl { get; set; } = string.Empty;
}
