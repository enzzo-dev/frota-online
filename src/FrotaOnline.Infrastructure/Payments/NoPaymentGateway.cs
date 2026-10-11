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

    public Task CancelChargeAsync(string chargeId, CancellationToken cancellationToken) => Fail();

    private static Task<string> Fail() => Fail<string>();

    private static Task<T> Fail<T>() => throw new PaymentGatewayException("No payment provider is configured.");
}
