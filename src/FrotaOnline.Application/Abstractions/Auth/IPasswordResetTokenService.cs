namespace FrotaOnline.Application.Abstractions.Auth;

public sealed record PasswordResetClaims(string Email, DateTime ExpiresAt, string PasswordFingerprint);

/// <summary>
/// Creates and reads stateless, signed password-reset tokens. A token carries the e-mail, its expiry and a
/// fingerprint of the password hash it was issued for, so it stops working as soon as the password changes.
/// </summary>
public interface IPasswordResetTokenService
{
    string Create(string email, string passwordHash, DateTime expiresAt);

    /// <summary>Returns the claims, or null when the token is malformed or its signature is wrong. Expiry is not checked here.</summary>
    PasswordResetClaims? Read(string token);

    string Fingerprint(string passwordHash);
}
