namespace FrotaOnline.Application.Auth;

public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    public int TokenMinutes { get; set; } = 30;

    public string ResetUrl { get; set; } = string.Empty;
}
