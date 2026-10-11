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

/// <summary>Raised when the provider refuses or cannot be reached. The details are logged where it happens.</summary>
public sealed class PaymentGatewayException : Exception
{
    public PaymentGatewayException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

/// <summary>
/// The payment provider (Asaas). The app never sees card data: charges are paid on the provider's page or by Pix.
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

    Task CancelChargeAsync(string chargeId, CancellationToken cancellationToken);
}

/// <summary>Checks that a webhook call really comes from the provider (shared secret in a header).</summary>
public interface IPaymentWebhookAuthenticator
{
    bool IsValid(string? token);
}
