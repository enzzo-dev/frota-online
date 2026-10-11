using FrotaOnline.Application.Abstractions.Notifications;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class NotificationPreferenceRepository : INotificationPreferenceRepository
{
    private readonly AppDbContext _db;

    public NotificationPreferenceRepository(AppDbContext db) => _db = db;

    public Task<NotificationPreference?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        _db.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public async Task AddAsync(NotificationPreference preference, CancellationToken cancellationToken) =>
        await _db.NotificationPreferences.AddAsync(preference, cancellationToken);

    public async Task<IReadOnlySet<Guid>> ListPushDisabledAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        (await _db.NotificationPreferences.AsNoTracking()
            .Where(p => !p.PushEnabled && userIds.Contains(p.UserId))
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken)).ToHashSet();
}
