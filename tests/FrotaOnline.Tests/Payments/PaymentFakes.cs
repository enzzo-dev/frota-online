using FrotaOnline.Application.Abstractions.Payments;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Tests.Payments;

internal sealed class FakeSubscriptionSettings : ISubscriptionSettings
{
    public bool RequiredToPropose { get; set; }
}

internal sealed class FakeSubscriptionRepository : ISubscriptionRepository
{
    public List<Subscription> Items { get; } = new();

    public Task<Subscription?> GetCurrentAsync(Guid driverProfileId, DateTime utcNow, CancellationToken cancellationToken) =>
        Task.FromResult(Items
            .Where(s => s.DriverProfileId == driverProfileId
                        && (s.Status != SubscriptionStatus.Cancelled || (s.EndsAt != null && s.EndsAt > utcNow)))
            .LastOrDefault());

    public Task<Subscription?> GetByGatewayIdAsync(string gatewaySubscriptionId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(s => s.GatewaySubscriptionId == gatewaySubscriptionId));

    public Task AddAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        Items.Add(subscription);
        return Task.CompletedTask;
    }
}

internal sealed class FakeFeeChargeRepository : IFeeChargeRepository
{
    public List<FeeCharge> Items { get; } = new();

    public Task<FeeCharge?> GetPendingByDriverAsync(Guid driverProfileId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.LastOrDefault(c => c.DriverProfileId == driverProfileId && c.Status == FeeChargeStatus.Pending));

    public Task<FeeCharge?> GetByGatewayIdAsync(string gatewayPaymentId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(c => c.GatewayPaymentId == gatewayPaymentId));

    public Task AddAsync(FeeCharge charge, CancellationToken cancellationToken)
    {
        Items.Add(charge);
        return Task.CompletedTask;
    }
}

internal sealed class FakeWebhookEventRepository : IWebhookEventRepository
{
    public List<WebhookEvent> Items { get; } = new();

    public Task<bool> ExistsAsync(string eventId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(e => e.EventId == eventId));

    public Task AddAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken)
    {
        Items.Add(webhookEvent);
        return Task.CompletedTask;
    }
}

internal sealed class FakePaymentGateway : IPaymentGateway
{
    private int _counter;

    public bool IsConfigured { get; set; } = true;

    public bool Fails { get; set; }

    public List<GatewayCustomer> Customers { get; } = new();

    public List<(string CustomerId, decimal Value, string ExternalReference)> Subscriptions { get; } = new();

    public List<PixChargeRequest> PixCharges { get; } = new();

    public List<string> CancelledSubscriptions { get; } = new();

    public List<string> CancelledCharges { get; } = new();

    /// <summary>When the next Pix stops working.</summary>
    public DateTime PixExpiresAt { get; set; } = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private void ThrowIfFailing()
    {
        if (Fails)
        {
            throw new PaymentGatewayException("provider down");
        }
    }

    public Task<string> CreateCustomerAsync(GatewayCustomer customer, CancellationToken cancellationToken)
    {
        ThrowIfFailing();
        Customers.Add(customer);
        return Task.FromResult($"cus_{++_counter}");
    }

    public Task<GatewaySubscription> CreateSubscriptionAsync(
        string customerId, decimal value, DateOnly firstDueDate, string description, string externalReference,
        CancellationToken cancellationToken)
    {
        ThrowIfFailing();
        Subscriptions.Add((customerId, value, externalReference));
        var id = $"sub_{++_counter}";
        return Task.FromResult(new GatewaySubscription(id, $"https://pay.example/{id}"));
    }

    public Task CancelSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        ThrowIfFailing();
        CancelledSubscriptions.Add(subscriptionId);
        return Task.CompletedTask;
    }

    public Task<PixCharge> CreatePixChargeAsync(PixChargeRequest request, CancellationToken cancellationToken)
    {
        ThrowIfFailing();
        PixCharges.Add(request);
        var id = $"pay_{++_counter}";
        return Task.FromResult(new PixCharge(id, $"000201pix{id}", "base64png", PixExpiresAt));
    }

    public Task CancelChargeAsync(string chargeId, CancellationToken cancellationToken)
    {
        ThrowIfFailing();
        CancelledCharges.Add(chargeId);
        return Task.CompletedTask;
    }
}
