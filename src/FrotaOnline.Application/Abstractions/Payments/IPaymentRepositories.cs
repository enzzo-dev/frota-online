using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Abstractions.Payments;

public interface ISubscriptionRepository
{
    /// <summary>The newest subscription that is not cancelled, or a cancelled one still inside its paid period.</summary>
    Task<Subscription?> GetCurrentAsync(Guid driverProfileId, DateTime utcNow, CancellationToken cancellationToken);

    Task<Subscription?> GetByGatewayIdAsync(string gatewaySubscriptionId, CancellationToken cancellationToken);

    Task AddAsync(Subscription subscription, CancellationToken cancellationToken);
}

public interface IFeeChargeRepository
{
    Task<FeeCharge?> GetPendingByDriverAsync(Guid driverProfileId, CancellationToken cancellationToken);

    Task<FeeCharge?> GetByGatewayIdAsync(string gatewayPaymentId, CancellationToken cancellationToken);

    Task AddAsync(FeeCharge charge, CancellationToken cancellationToken);
}

public interface IWebhookEventRepository
{
    Task<bool> ExistsAsync(string eventId, CancellationToken cancellationToken);

    Task AddAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken);
}

/// <summary>Whether a plan is needed to send proposals. Read from configuration so it can be switched without a deploy of code.</summary>
public interface ISubscriptionSettings
{
    bool RequiredToPropose { get; }
}

public interface ISavedCardRepository
{
    Task<IReadOnlyList<SavedCard>> ListByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<SavedCard?> GetAsync(Guid userId, Guid cardId, CancellationToken cancellationToken);

    Task AddAsync(SavedCard card, CancellationToken cancellationToken);

    void Remove(SavedCard card);
}

public interface IWalletAccountRepository
{
    Task<WalletAccount?> GetByDriverAsync(Guid driverProfileId, CancellationToken cancellationToken);

    Task AddAsync(WalletAccount account, CancellationToken cancellationToken);

    void Remove(WalletAccount account);
}
