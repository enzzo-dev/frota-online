using FrotaOnline.Application.Abstractions.Notifications;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class DeviceTokenRepository : IDeviceTokenRepository
{
    private readonly AppDbContext _db;

    public DeviceTokenRepository(AppDbContext db) => _db = db;

    public Task<DeviceToken?> GetByTokenAsync(string token, CancellationToken cancellationToken) =>
        _db.DeviceTokens.FirstOrDefaultAsync(t => t.Token == token, cancellationToken);

    public async Task<IReadOnlyList<DeviceToken>> ListByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        await _db.DeviceTokens.AsNoTracking().Where(t => userIds.Contains(t.UserId)).ToListAsync(cancellationToken);

    public async Task AddAsync(DeviceToken token, CancellationToken cancellationToken) =>
        await _db.DeviceTokens.AddAsync(token, cancellationToken);

    public void Remove(DeviceToken token) => _db.DeviceTokens.Remove(token);

    public Task<int> RemoveByTokensAsync(IReadOnlyCollection<string> tokens, CancellationToken cancellationToken) =>
        _db.DeviceTokens.Where(t => tokens.Contains(t.Token)).ExecuteDeleteAsync(cancellationToken);
}
