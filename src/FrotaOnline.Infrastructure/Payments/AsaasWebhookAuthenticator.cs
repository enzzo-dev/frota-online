using System.Security.Cryptography;
using System.Text;
using FrotaOnline.Application.Abstractions.Payments;
using Microsoft.Extensions.Options;

namespace FrotaOnline.Infrastructure.Payments;

internal sealed class AsaasWebhookAuthenticator : IPaymentWebhookAuthenticator
{
    private readonly byte[] _expected;

    public AsaasWebhookAuthenticator(IOptions<PaymentOptions> options) =>
        _expected = Encoding.UTF8.GetBytes(options.Value.Asaas.WebhookToken);

    /// <summary>Constant-time comparison. An empty configured token refuses everything.</summary>
    public bool IsValid(string? token)
    {
        if (_expected.Length == 0 || string.IsNullOrEmpty(token))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), _expected);
    }
}
