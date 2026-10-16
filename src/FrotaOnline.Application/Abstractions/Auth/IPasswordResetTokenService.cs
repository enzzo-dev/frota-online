namespace FrotaOnline.Application.Abstractions.Auth;

public sealed record PasswordResetClaims(string Email, DateTime ExpiresAt, string PasswordFingerprint);

public interface IPasswordResetTokenService
{
    string Create(string email, string passwordHash, DateTime expiresAt);

    PasswordResetClaims? Read(string token);

    string Fingerprint(string passwordHash);
}
