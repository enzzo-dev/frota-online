using FrotaOnline.Application.Abstractions.Privacy;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Privacy;

/// <summary>
/// Erases a person in one transaction. Set-based statements (ExecuteUpdate / ExecuteDelete) clean the tables
/// that can be large; the few rows that identify the person go through the entities so the domain rules
/// (<c>Anonymize</c>) decide what the placeholders look like.
/// </summary>
internal sealed class AccountErasureStore : IAccountErasureStore
{
    private static readonly DeliveryStatus[] FinishedStatuses = { DeliveryStatus.Delivered, DeliveryStatus.Cancelled };

    private readonly AppDbContext _db;

    public AccountErasureStore(AppDbContext db) => _db = db;

    public Task<bool> HasActiveDeliveriesAsync(Guid userId, CancellationToken cancellationToken) =>
        (from d in _db.Deliveries.AsNoTracking()
         where !FinishedStatuses.Contains(d.Status)
         join p in _db.DriverProfiles.AsNoTracking() on d.DriverProfileId equals p.Id
         where d.CustomerId == userId || p.UserId == userId
         select d.Id)
        .AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> ListOpenSubscriptionIdsAsync(Guid userId, CancellationToken cancellationToken) =>
        await (from s in _db.Subscriptions.AsNoTracking()
               join d in _db.DriverProfiles.AsNoTracking() on s.DriverProfileId equals d.Id
               where d.UserId == userId
                     && s.Status != SubscriptionStatus.Cancelled
                     && s.GatewaySubscriptionId != null
               select s.GatewaySubscriptionId!)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> EraseAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var user = await _db.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        var driver = await _db.DriverProfiles.FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken);
        var driverId = driver?.Id;

        // 1. Close what is still open. Proposals first: they are found through the open freights.
        await _db.Proposals
            .Where(p => p.Status == ProposalStatus.Pending
                        && _db.FreightRequests.Any(f => f.Id == p.FreightRequestId
                                                        && f.CustomerId == userId
                                                        && f.Status == FreightStatus.Open))
            .ExecuteUpdateAsync(
                set => set.SetProperty(p => p.Status, ProposalStatus.Rejected).SetProperty(p => p.UpdatedAt, utcNow),
                cancellationToken);

        await _db.FreightRequests
            .Where(f => f.CustomerId == userId && f.Status == FreightStatus.Open)
            .ExecuteUpdateAsync(
                set => set.SetProperty(f => f.Status, FreightStatus.Cancelled).SetProperty(f => f.UpdatedAt, utcNow),
                cancellationToken);

        if (driverId is not null)
        {
            await _db.Proposals
                .Where(p => p.DriverProfileId == driverId && p.Status == ProposalStatus.Pending)
                .ExecuteUpdateAsync(
                    set => set.SetProperty(p => p.Status, ProposalStatus.Withdrawn).SetProperty(p => p.UpdatedAt, utcNow),
                    cancellationToken);

            // The subscription keeps the driver's phone number: blank it and end the plan.
            await _db.Subscriptions
                .Where(x => x.DriverProfileId == driverId)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(x => x.PhoneNumber, "0")
                        .SetProperty(x => x.PaymentUrl, (string?)null)
                        .SetProperty(x => x.Status, SubscriptionStatus.Cancelled)
                        .SetProperty(x => x.EndsAt, utcNow)
                        .SetProperty(x => x.UpdatedAt, utcNow),
                    cancellationToken);

            await _db.FeeCharges
                .Where(c => c.DriverProfileId == driverId && c.Status == FeeChargeStatus.Pending)
                .ExecuteUpdateAsync(
                    set => set.SetProperty(c => c.Status, FeeChargeStatus.Cancelled).SetProperty(c => c.UpdatedAt, utcNow),
                    cancellationToken);

            await _db.Proposals
                .Where(p => p.DriverProfileId == driverId && p.Message != null)
                .ExecuteUpdateAsync(set => set.SetProperty(p => p.Message, (string?)null), cancellationToken);
        }

        // 2. Free text and coordinates the person typed or generated, in rows that must stay.
        await _db.FreightStops
            .Where(s => s.Note != null && _db.FreightRequests.Any(f => f.Id == s.FreightRequestId && f.CustomerId == userId))
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.Note, (string?)null), cancellationToken);

        await _db.DeliveryEvents
            .Where(e => _db.Deliveries.Any(d => d.Id == e.DeliveryId
                                                && (d.CustomerId == userId || d.DriverProfileId == driverId)))
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(e => e.Note, (string?)null)
                    .SetProperty(e => e.Latitude, (double?)null)
                    .SetProperty(e => e.Longitude, (double?)null),
                cancellationToken);

        await _db.DeliveryLocations
            .Where(l => _db.Deliveries.Any(d => d.Id == l.DeliveryId
                                                && (d.CustomerId == userId || d.DriverProfileId == driverId)))
            .ExecuteDeleteAsync(cancellationToken);

        await _db.UserConsents
            .Where(c => c.UserId == userId && c.IpAddress != null)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.IpAddress, (string?)null), cancellationToken);

        // 3. Rows with no reason to stay. Delivery proofs point to media, so detach them before deleting the files.
        await _db.DeliveryEvents
            .Where(e => e.MediaFileId != null
                        && _db.MediaFiles.Any(m => m.Id == e.MediaFileId && m.OwnerUserId == userId))
            .ExecuteUpdateAsync(set => set.SetProperty(e => e.MediaFileId, (Guid?)null), cancellationToken);

        var storageKeys = await _db.MediaFiles
            .AsNoTracking()
            .Where(m => m.OwnerUserId == userId)
            .Select(m => m.StorageKey)
            .ToListAsync(cancellationToken);
        await _db.Signatures
            .Where(s => s.CustomerId == userId || s.DriverProfileId == driverId)
            .ExecuteDeleteAsync(cancellationToken);
        await _db.MediaFiles.Where(m => m.OwnerUserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _db.RefreshTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _db.DeviceTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _db.Notifications.Where(n => n.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _db.NotificationPreferences.Where(p => p.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _db.SavedCards.Where(c => c.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        if (driverId is not null)
        {
            await _db.WalletAccounts.Where(a => a.DriverProfileId == driverId).ExecuteDeleteAsync(cancellationToken);
        }

        await _db.CustomerProfiles.Where(c => c.UserId == userId).ExecuteDeleteAsync(cancellationToken);

        // 4. Rows that stay but must stop identifying the person: through the domain.
        var addresses = await _db.Addresses.Where(a => a.OwnerUserId == userId).ToListAsync(cancellationToken);
        addresses.ForEach(a => a.Anonymize());

        if (driver is not null)
        {
            var vehicles = await _db.Vehicles.Where(v => v.DriverProfileId == driver.Id).ToListAsync(cancellationToken);
            vehicles.ForEach(v => v.Anonymize());
            driver.Anonymize();
        }

        user.Anonymize(utcNow);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return storageKeys;
    }
}
