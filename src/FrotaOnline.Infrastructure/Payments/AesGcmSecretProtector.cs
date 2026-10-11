using System.Security.Cryptography;
using System.Text;
using FrotaOnline.Application.Abstractions.Payments;
using Microsoft.Extensions.Options;

namespace FrotaOnline.Infrastructure.Payments;

/// <summary>
/// AES-256-GCM with a key from configuration (not a key ring, so it survives restarts and new instances). The output is
/// base64url of nonce + tag + ciphertext; any change to it fails the tag check.
/// </summary>
internal sealed class AesGcmSecretProtector : ISecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public AesGcmSecretProtector(IOptions<PaymentOptions> options)
    {
        var text = options.Value.MercadoPago.TokenEncryptionKey;
        try
        {
            _key = Convert.FromBase64String(text);
        }
        catch (FormatException)
        {
            _key = Array.Empty<byte>();
        }

        if (_key.Length != 32)
        {
            throw new InvalidOperationException("Payments:MercadoPago:TokenEncryptionKey must be the base64 of 32 bytes.");
        }
    }

    public string Protect(string plainText)
    {
        var plain = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        var packed = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(packed, 0);
        tag.CopyTo(packed, NonceSize);
        cipher.CopyTo(packed, NonceSize + TagSize);
        return Base64Url(packed);
    }

    public bool TryUnprotect(string protectedText, out string plainText)
    {
        plainText = string.Empty;
        try
        {
            var packed = FromBase64Url(protectedText);
            if (packed.Length < NonceSize + TagSize)
            {
                return false;
            }

            var nonce = packed.AsSpan(0, NonceSize);
            var tag = packed.AsSpan(NonceSize, TagSize);
            var cipher = packed.AsSpan(NonceSize + TagSize);
            var plain = new byte[cipher.Length];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);
            plainText = Encoding.UTF8.GetString(plain);
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            return false;
        }
    }

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
