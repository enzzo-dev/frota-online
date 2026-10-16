namespace FrotaOnline.Application.Abstractions.Payments;

public sealed record GatewayCustomer(string Name, string Email, string TaxId, string? Phone, string ExternalReference);

public sealed record GatewaySubscription(string Id, string? PaymentUrl);

/// <summary>Part of a received charge that goes straight to another account of the provider.</summary>
public sealed record GatewaySplit(string WalletId, decimal FixedValue);

public sealed record PixChargeRequest(
    string CustomerId,
    decimal Value,
    DateOnly DueDate,
    string Description,
    string ExternalReference,
    IReadOnlyList<GatewaySplit> Splits);

/// <summary>A Pix charge ready to be paid: the "copia e cola" code, the QR image (base64) and when it stops working.</summary>
public sealed record PixCharge(string Id, string Payload, string? QrCodeImage, DateTime ExpiresAt);

/// <summary>Card data on its way to the provider. Never stored and never logged.</summary>
public sealed record CardDetails(string HolderName, string Number, string ExpiryMonth, string ExpiryYear, string Ccv);

public sealed record CardHolderDetails(string Name, string Email, string TaxId, string PostalCode, string AddressNumber, string? Phone);

/// <summary>
/// A card charge. Either <see cref="Card"/> and <see cref="Holder"/> (a card typed in now) or <see cref="CardToken"/>
/// (a card kept earlier). <see cref="RemoteIp"/> is the payer's device address, which the provider requires for fraud checks.
/// </summary>
public sealed record CardChargeRequest(
    string CustomerId,
    decimal Value,
    DateOnly DueDate,
    string Description,
    string ExternalReference,
    IReadOnlyList<GatewaySplit> Splits,
    string RemoteIp,
    CardDetails? Card,
    CardHolderDetails? Holder,
    string? CardToken);

/// <summary>An approved card charge. <see cref="Token"/> is set when the provider returned one to keep the card.</summary>
public sealed record CardCharge(string Id, string Status, string? Token, string? Brand, string? Last4);

/// <summary>Raised when the provider refuses or cannot be reached. The details are logged where it happens.</summary>
public class PaymentGatewayException : Exception
{
    public PaymentGatewayException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

/// <summary>The provider answered but did not accept the card (declined, wrong data, no limit).</summary>
public sealed class PaymentDeclinedException : PaymentGatewayException
{
    public PaymentDeclinedException(string message) : base(message)
    {
    }
}

/// <summary>
/// The payment provider (Asaas): subscriptions (paid on the provider's page), Pix and card charges. Card data passes
/// through to the provider in one request and is never stored or logged.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>False when no provider is set up in this environment (<c>Payments:Provider</c> = None).</summary>
    bool IsConfigured { get; }

    Task<string> CreateCustomerAsync(GatewayCustomer customer, CancellationToken cancellationToken);

    /// <summary>Monthly charge the driver can pay by Pix, boleto or card on the provider's page.</summary>
    Task<GatewaySubscription> CreateSubscriptionAsync(
        string customerId, decimal value, DateOnly firstDueDate, string description, string externalReference,
        CancellationToken cancellationToken);

    Task CancelSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken);

    Task<PixCharge> CreatePixChargeAsync(PixChargeRequest request, CancellationToken cancellationToken);

    /// <summary>Charges a card. Throws <see cref="PaymentDeclinedException"/> when the card is not accepted.</summary>
    Task<CardCharge> CreateCardChargeAsync(CardChargeRequest request, CancellationToken cancellationToken);

    Task CancelChargeAsync(string chargeId, CancellationToken cancellationToken);
}

/// <summary>The driver's Mercado Pago account, authorised through OAuth so payments can be created on their behalf.</summary>
public sealed record SellerAuthorization(string ProviderUserId, string AccessToken, string RefreshToken, DateTime ExpiresAt);

/// <summary>
/// A payment made with a token the app produced (Apple Pay through the provider's SDK, or a card). It is created with the
/// driver's own access token, so the driver is the seller and the platform keeps only <see cref="PlatformFee"/>.
/// </summary>
public sealed record WalletChargeRequest(
    string SellerAccessToken,
    string CardToken,
    string PaymentMethodId,
    string PayerEmail,
    decimal Value,
    decimal PlatformFee,
    string Description,
    string ExternalReference,
    string IdempotencyKey);

/// <summary>Provider status: <c>approved</c>, <c>in_process</c>, <c>pending</c>, <c>rejected</c>, <c>cancelled</c>.</summary>
public sealed record WalletCharge(string Id, string Status);

/// <summary>
/// Mercado Pago: wallet payments (Apple Pay) and the split with the driver. The driver connects their account once with OAuth;
/// the app never handles their credentials.
/// </summary>
public interface IWalletPaymentGateway
{
    bool IsConfigured { get; }

    /// <summary>Page where the driver allows the platform to receive payments for them. <paramref name="state"/> comes back on the callback.</summary>
    string BuildAuthorizationUrl(string state);

    Task<SellerAuthorization> ExchangeCodeAsync(string code, CancellationToken cancellationToken);

    Task<SellerAuthorization> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>Throws <see cref="PaymentDeclinedException"/> when the payment is rejected.</summary>
    Task<WalletCharge> ChargeAsync(WalletChargeRequest request, CancellationToken cancellationToken);

    Task<WalletCharge> GetPaymentAsync(string paymentId, string sellerAccessToken, CancellationToken cancellationToken);
}

/// <summary>Checks that a Mercado Pago notification is signed with the app's secret.</summary>
public interface IWalletWebhookAuthenticator
{
    bool IsValid(string? signatureHeader, string? requestId, string? dataId);
}

/// <summary>Encrypts secrets (provider tokens) before they reach the database, and signs short-lived values.</summary>
public interface ISecretProtector
{
    /// <summary>URL-safe text. Tampering is detected on <see cref="TryUnprotect"/>.</summary>
    string Protect(string plainText);

    bool TryUnprotect(string protectedText, out string plainText);
}

/// <summary>Checks that a webhook call really comes from the provider (shared secret in a header).</summary>
public interface IPaymentWebhookAuthenticator
{
    bool IsValid(string? token);
}
