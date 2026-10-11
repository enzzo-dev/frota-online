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

    public List<CardChargeRequest> CardCharges { get; } = new();

    /// <summary>When true the next card charge is refused by the provider.</summary>
    public bool DeclineCards { get; set; }

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

    public Task<CardCharge> CreateCardChargeAsync(CardChargeRequest request, CancellationToken cancellationToken)
    {
        ThrowIfFailing();
        if (DeclineCards)
        {
            throw new PaymentDeclinedException("declined");
        }

        CardCharges.Add(request);
        var id = $"pay_{++_counter}";
        return Task.FromResult(new CardCharge(id, "CONFIRMED", $"tok_{id}", "VISA", "1111"));
    }

    public Task CancelChargeAsync(string chargeId, CancellationToken cancellationToken)
    {
        ThrowIfFailing();
        CancelledCharges.Add(chargeId);
        return Task.CompletedTask;
    }
}

internal sealed class FakeSavedCardRepository : ISavedCardRepository
{
    public List<SavedCard> Items { get; } = new();

    public Task<IReadOnlyList<SavedCard>> ListByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SavedCard>>(Items.Where(c => c.UserId == userId).ToList());

    public Task<SavedCard?> GetAsync(Guid userId, Guid cardId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(c => c.UserId == userId && c.Id == cardId));

    public Task AddAsync(SavedCard card, CancellationToken cancellationToken)
    {
        Items.Add(card);
        return Task.CompletedTask;
    }

    public void Remove(SavedCard card) => Items.Remove(card);
}

internal sealed class FakeWalletAccountRepository : IWalletAccountRepository
{
    public List<WalletAccount> Items { get; } = new();

    public Task<WalletAccount?> GetByDriverAsync(Guid driverProfileId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(a => a.DriverProfileId == driverProfileId));

    public Task AddAsync(WalletAccount account, CancellationToken cancellationToken)
    {
        Items.Add(account);
        return Task.CompletedTask;
    }

    public void Remove(WalletAccount account) => Items.Remove(account);
}

/// <summary>Not real encryption: a reversible marker, enough to check that tokens are never stored as received.</summary>
internal sealed class FakeSecretProtector : ISecretProtector
{
    public string Protect(string plainText) => "enc:" + plainText;

    public bool TryUnprotect(string protectedText, out string plainText)
    {
        var ok = protectedText.StartsWith("enc:", StringComparison.Ordinal);
        plainText = ok ? protectedText[4..] : string.Empty;
        return ok;
    }
}

internal sealed class FakeWalletGateway : IWalletPaymentGateway
{
    private int _counter;

    public bool IsConfigured { get; set; } = true;

    public bool Fails { get; set; }

    public bool Declines { get; set; }

    /// <summary>Status the provider answers for the next charge and for later reads of it.</summary>
    public string Status { get; set; } = "approved";

    public List<WalletChargeRequest> Charges { get; } = new();

    public List<string> Refreshed { get; } = new();

    public DateTime TokenExpiresAt { get; set; } = new(2027, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    public string BuildAuthorizationUrl(string state) => $"https://auth.example/authorize?state={state}";

    public Task<SellerAuthorization> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        if (Fails || code == "bad")
        {
            throw new PaymentGatewayException("provider down");
        }

        return Task.FromResult(new SellerAuthorization("mp-user-1", "access-1", "refresh-1", TokenExpiresAt));
    }

    public Task<SellerAuthorization> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        Refreshed.Add(refreshToken);
        return Task.FromResult(new SellerAuthorization("mp-user-1", "access-2", "refresh-2", TokenExpiresAt));
    }

    public Task<WalletCharge> ChargeAsync(WalletChargeRequest request, CancellationToken cancellationToken)
    {
        if (Fails)
        {
            throw new PaymentGatewayException("provider down");
        }

        if (Declines)
        {
            throw new PaymentDeclinedException("rejected");
        }

        Charges.Add(request);
        return Task.FromResult(new WalletCharge($"mp_{++_counter}", Status));
    }

    public Task<WalletCharge> GetPaymentAsync(string paymentId, string sellerAccessToken, CancellationToken cancellationToken) =>
        Task.FromResult(new WalletCharge(paymentId, Status));
}
