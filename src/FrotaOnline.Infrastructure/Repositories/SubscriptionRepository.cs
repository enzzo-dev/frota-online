using FrotaOnline.Application.Abstractions.Payments;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class SubscriptionRepository : ISubscriptionRepository
{
    private readonly AppDbContext _db;

    public SubscriptionRepository(AppDbContext db) => _db = db;

    public Task<Subscription?> GetCurrentAsync(Guid driverProfileId, DateTime utcNow, CancellationToken cancellationToken) =>
        _db.Subscriptions
            .Where(s => s.DriverProfileId == driverProfileId
                        && (s.Status != SubscriptionStatus.Cancelled || (s.EndsAt != null && s.EndsAt > utcNow)))
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Subscription?> GetByGatewayIdAsync(string gatewaySubscriptionId, CancellationToken cancellationToken) =>
        _db.Subscriptions.FirstOrDefaultAsync(s => s.GatewaySubscriptionId == gatewaySubscriptionId, cancellationToken);

    public async Task AddAsync(Subscription subscription, CancellationToken cancellationToken) =>
        await _db.Subscriptions.AddAsync(subscription, cancellationToken);
}

internal sealed class FeeChargeRepository : IFeeChargeRepository
{
    private readonly AppDbContext _db;

    public FeeChargeRepository(AppDbContext db) => _db = db;

    public Task<FeeCharge?> GetPendingByDriverAsync(Guid driverProfileId, CancellationToken cancellationToken) =>
        _db.FeeCharges
            .Where(c => c.DriverProfileId == driverProfileId && c.Status == FeeChargeStatus.Pending)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<FeeCharge?> GetByGatewayIdAsync(string gatewayPaymentId, CancellationToken cancellationToken) =>
        _db.FeeCharges.FirstOrDefaultAsync(c => c.GatewayPaymentId == gatewayPaymentId, cancellationToken);

    public async Task AddAsync(FeeCharge charge, CancellationToken cancellationToken) =>
        await _db.FeeCharges.AddAsync(charge, cancellationToken);
}

internal sealed class WebhookEventRepository : IWebhookEventRepository
{
    private readonly AppDbContext _db;

    public WebhookEventRepository(AppDbContext db) => _db = db;

    public Task<bool> ExistsAsync(string eventId, CancellationToken cancellationToken) =>
        _db.WebhookEvents.AnyAsync(e => e.EventId == eventId, cancellationToken);

    public async Task AddAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken) =>
        await _db.WebhookEvents.AddAsync(webhookEvent, cancellationToken);
}

internal sealed class SavedCardRepository : ISavedCardRepository
{
    private readonly AppDbContext _db;

    public SavedCardRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<SavedCard>> ListByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await _db.SavedCards.Where(c => c.UserId == userId).OrderBy(c => c.CreatedAt).ToListAsync(cancellationToken);

    public Task<SavedCard?> GetAsync(Guid userId, Guid cardId, CancellationToken cancellationToken) =>
        _db.SavedCards.FirstOrDefaultAsync(c => c.UserId == userId && c.Id == cardId, cancellationToken);

    public async Task AddAsync(SavedCard card, CancellationToken cancellationToken) =>
        await _db.SavedCards.AddAsync(card, cancellationToken);

    public void Remove(SavedCard card) => _db.SavedCards.Remove(card);
}

internal sealed class WalletAccountRepository : IWalletAccountRepository
{
    private readonly AppDbContext _db;

    public WalletAccountRepository(AppDbContext db) => _db = db;

    public Task<WalletAccount?> GetByDriverAsync(Guid driverProfileId, CancellationToken cancellationToken) =>
        _db.WalletAccounts.FirstOrDefaultAsync(a => a.DriverProfileId == driverProfileId, cancellationToken);

    public async Task AddAsync(WalletAccount account, CancellationToken cancellationToken) =>
        await _db.WalletAccounts.AddAsync(account, cancellationToken);

    public void Remove(WalletAccount account) => _db.WalletAccounts.Remove(account);
}
