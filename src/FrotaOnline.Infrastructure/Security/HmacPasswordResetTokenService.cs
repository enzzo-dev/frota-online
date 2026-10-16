using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FrotaOnline.Application.Abstractions.Auth;
using Microsoft.Extensions.Options;

namespace FrotaOnline.Infrastructure.Security;

/// <summary>
/// Token format: base64url(json payload) + "." + base64url(HMAC-SHA256 signature). The signing key is derived from
/// the JWT secret with a purpose label, so a reset token can never be confused with (or forged from) an access token.
/// </summary>
public sealed class HmacPasswordResetTokenService : IPasswordResetTokenService
{
    private const string Purpose = "frota-online:password-reset:v1";

    private readonly byte[] _key;

    public HmacPasswordResetTokenService(IOptions<JwtOptions> jwtOptions)
    {
        _key = HMACSHA256.HashData(Encoding.UTF8.GetBytes(jwtOptions.Value.SecretKey), Encoding.UTF8.GetBytes(Purpose));
    }

    public string Create(string email, string passwordHash, DateTime expiresAt)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new Payload(
            email,
            new DateTimeOffset(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc)).ToUnixTimeSeconds(),
            Fingerprint(passwordHash)));

        return $"{Base64Url(payload)}.{Base64Url(Sign(payload))}";
    }

    public PasswordResetClaims? Read(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 2 || !TryDecode(parts[0], out var payload) || !TryDecode(parts[1], out var signature))
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(Sign(payload), signature))
        {
            return null;
        }

        try
        {
            var data = JsonSerializer.Deserialize<Payload>(payload);
            if (data is null || string.IsNullOrWhiteSpace(data.Email) || string.IsNullOrEmpty(data.Fingerprint))
            {
                return null;
            }

            return new PasswordResetClaims(data.Email, DateTimeOffset.FromUnixTimeSeconds(data.Expires).UtcDateTime, data.Fingerprint);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string Fingerprint(string passwordHash) =>
        Base64Url(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(passwordHash)));

    private byte[] Sign(byte[] payload) => HMACSHA256.HashData(_key, payload);

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryDecode(string value, out byte[] bytes)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        try
        {
            bytes = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }

    private sealed record Payload(
        [property: System.Text.Json.Serialization.JsonPropertyName("e")] string Email,
        [property: System.Text.Json.Serialization.JsonPropertyName("x")] long Expires,
        [property: System.Text.Json.Serialization.JsonPropertyName("f")] string Fingerprint);
}
