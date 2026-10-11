using FrotaOnline.Application.Abstractions.Payments;

namespace FrotaOnline.Infrastructure.Payments;

/// <summary>Used when <c>Payments:Provider</c> is not set: every online payment feature answers "not configured".</summary>
internal sealed class NoPaymentGateway : IPaymentGateway
{
    public bool IsConfigured => false;

    public Task<string> CreateCustomerAsync(GatewayCustomer customer, CancellationToken cancellationToken) => Fail();

    public Task<GatewaySubscription> CreateSubscriptionAsync(
        string customerId, decimal value, DateOnly firstDueDate, string description, string externalReference,
        CancellationToken cancellationToken) => Fail<GatewaySubscription>();

    public Task CancelSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken) => Fail();

    public Task<PixCharge> CreatePixChargeAsync(PixChargeRequest request, CancellationToken cancellationToken) => Fail<PixCharge>();

    public Task<CardCharge> CreateCardChargeAsync(CardChargeRequest request, CancellationToken cancellationToken) => Fail<CardCharge>();

    public Task CancelChargeAsync(string chargeId, CancellationToken cancellationToken) => Fail();

    private static Task<string> Fail() => Fail<string>();

    private static Task<T> Fail<T>() => throw new PaymentGatewayException("No payment provider is configured.");
}

/// <summary>Used when <c>Payments:WalletProvider</c> is not set: wallet payments answer "not configured".</summary>
internal sealed class NoWalletPaymentGateway : IWalletPaymentGateway
{
    public bool IsConfigured => false;

    public string BuildAuthorizationUrl(string state) => throw new PaymentGatewayException("No wallet provider is configured.");

    public Task<SellerAuthorization> ExchangeCodeAsync(string code, CancellationToken cancellationToken) => Fail<SellerAuthorization>();

    public Task<SellerAuthorization> RefreshAsync(string refreshToken, CancellationToken cancellationToken) => Fail<SellerAuthorization>();

    public Task<WalletCharge> ChargeAsync(WalletChargeRequest request, CancellationToken cancellationToken) => Fail<WalletCharge>();

    public Task<WalletCharge> GetPaymentAsync(string paymentId, string sellerAccessToken, CancellationToken cancellationToken) =>
        Fail<WalletCharge>();

    private static Task<T> Fail<T>() => throw new PaymentGatewayException("No wallet provider is configured.");
}

/// <summary>Without a wallet provider there is nothing to protect: any call is a bug, so it fails loudly.</summary>
internal sealed class DisabledSecretProtector : ISecretProtector
{
    public string Protect(string plainText) => throw new InvalidOperationException("No wallet provider is configured.");

    public bool TryUnprotect(string protectedText, out string plainText)
    {
        plainText = string.Empty;
        return false;
    }
}

internal sealed class DisabledWalletWebhookAuthenticator : IWalletWebhookAuthenticator
{
    public bool IsValid(string? signatureHeader, string? requestId, string? dataId) => false;
}
