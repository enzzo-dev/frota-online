using FrotaOnline.Application.Abstractions.Notifications;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class NotificationRepository : INotificationRepository
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    private readonly AppDbContext _db;

    public NotificationRepository(AppDbContext db) => _db = db;

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Notifications.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken) =>
        _db.Notifications.AddRangeAsync(notifications, cancellationToken);

    public Task<int> MarkAllReadAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) =>
        _db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(
                set => set.SetProperty(n => n.ReadAt, utcNow).SetProperty(n => n.UpdatedAt, utcNow),
                cancellationToken);

    public async Task<IReadOnlyList<Notification>> ClaimPendingPushAsync(
        DateTime utcNow, int take, CancellationToken cancellationToken)
    {
        // One atomic statement: pick the rows with FOR UPDATE SKIP LOCKED (rows another instance is claiming right
        // now are skipped, not waited for) and stamp UpdatedAt = now. UpdatedAt doubles as the lease: a row is
        // offered again only RetryDelay after it was last touched, which is also the delay between retries.
        const string sql = """
            UPDATE "Notifications" SET "UpdatedAt" = @now
            WHERE "Id" IN (
                SELECT "Id" FROM "Notifications"
                WHERE "PushDispatchedAt" IS NULL AND "ReadAt" IS NULL
                  AND ("UpdatedAt" IS NULL OR "UpdatedAt" <= @retryBefore)
                ORDER BY "CreatedAt"
                LIMIT @take
                FOR UPDATE SKIP LOCKED)
            RETURNING "Id"
            """;

        var ids = new List<Guid>();
        await _db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = _db.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            AddParameter(command, "now", utcNow);
            AddParameter(command, "retryBefore", utcNow - RetryDelay);
            AddParameter(command, "take", take);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetGuid(0));
            }
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }

        if (ids.Count == 0)
        {
            return Array.Empty<Notification>();
        }

        return await _db.Notifications
            .Where(n => ids.Contains(n.Id))
            .OrderBy(n => n.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
