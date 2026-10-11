using System.Security.Cryptography;
using System.Text;
using FrotaOnline.Application.Abstractions.Payments;
using Microsoft.Extensions.Options;

namespace FrotaOnline.Infrastructure.Payments;

/// <summary>
/// Mercado Pago signs each notification: header <c>x-signature: ts=…,v1=…</c> where v1 is HMAC-SHA256 (hex) of
/// <c>id:&lt;data.id&gt;;request-id:&lt;x-request-id&gt;;ts:&lt;ts&gt;;</c> with the webhook secret.
/// </summary>
internal sealed class MercadoPagoWebhookAuthenticator : IWalletWebhookAuthenticator
{
    private readonly byte[] _secret;

    public MercadoPagoWebhookAuthenticator(IOptions<PaymentOptions> options) =>
        _secret = Encoding.UTF8.GetBytes(options.Value.MercadoPago.WebhookSecret);

    public bool IsValid(string? signatureHeader, string? requestId, string? dataId)
    {
        if (_secret.Length == 0 || string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(dataId))
        {
            return false;
        }

        string? ts = null, v1 = null;
        foreach (var part in signatureHeader.Split(','))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2)
            {
                continue;
            }

            switch (pair[0].Trim())
            {
                case "ts":
                    ts = pair[1].Trim();
                    break;
                case "v1":
                    v1 = pair[1].Trim();
                    break;
            }
        }

        if (ts is null || v1 is null)
        {
            return false;
        }

        // The provider lower-cases an alphanumeric id before signing it.
        var id = dataId.All(char.IsAsciiLetterOrDigit) ? dataId.ToLowerInvariant() : dataId;
        var manifest = new StringBuilder().Append("id:").Append(id).Append(';');
        if (!string.IsNullOrWhiteSpace(requestId))
        {
            manifest.Append("request-id:").Append(requestId).Append(';');
        }

        manifest.Append("ts:").Append(ts).Append(';');

        var expected = Convert.ToHexString(HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(manifest.ToString()))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(v1.ToLowerInvariant()));
    }
}
