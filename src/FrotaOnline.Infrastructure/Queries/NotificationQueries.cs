using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Notifications.Dtos;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Queries;

internal sealed class NotificationQueries : INotificationQueries
{
    private readonly AppDbContext _db;

    public NotificationQueries(AppDbContext db) => _db = db;

    public async Task<NotificationListResponse> ListAsync(
        Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAt == null);
        }

        // One extra row tells whether there is a next page without a COUNT over the whole inbox.
        var rows = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenBy(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize + 1)
            .Select(n => new NotificationResponse(
                n.Id, n.Type, n.Title, n.Body, n.TargetType, n.TargetId, n.ReadAt != null, n.CreatedAt, n.ReadAt))
            .ToListAsync(cancellationToken);

        var unread = await CountUnreadAsync(userId, cancellationToken);
        var hasMore = rows.Count > pageSize;
        return new NotificationListResponse(hasMore ? rows.Take(pageSize).ToList() : rows, unread, page, pageSize, hasMore);
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        _db.Notifications.AsNoTracking().CountAsync(n => n.UserId == userId && n.ReadAt == null, cancellationToken);
}
