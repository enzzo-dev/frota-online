using FrotaOnline.Application.Abstractions.Notifications;
using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Notifications.Dtos;
using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Tests.Notifications;

internal sealed class FakeNotificationRepository : INotificationRepository
{
    public List<Notification> Items { get; } = new();

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(n => n.Id == id));

    public Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken)
    {
        Items.AddRange(notifications);
        return Task.CompletedTask;
    }

    public Task<int> MarkAllReadAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var unread = Items.Where(n => n.UserId == userId && !n.IsRead).ToList();
        unread.ForEach(n => n.MarkRead(utcNow));
        return Task.FromResult(unread.Count);
    }

    public Task<IReadOnlyList<Notification>> ClaimPendingPushAsync(DateTime utcNow, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Notification>>(
            Items.Where(n => n.PushDispatchedAt is null && !n.IsRead).Take(take).ToList());
}

internal sealed class FakeNotificationQueries : INotificationQueries
{
    private readonly FakeNotificationRepository _repository;

    public FakeNotificationQueries(FakeNotificationRepository repository) => _repository = repository;

    public Task<NotificationListResponse> ListAsync(
        Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = _repository.Items
            .Where(n => n.UserId == userId && (!unreadOnly || !n.IsRead))
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize + 1)
            .Select(n => new NotificationResponse(
                n.Id, n.Type, n.Title, n.Body, n.TargetType, n.TargetId, n.IsRead, n.CreatedAt, n.ReadAt))
            .ToList();
        var hasMore = rows.Count > pageSize;
        var unread = _repository.Items.Count(n => n.UserId == userId && !n.IsRead);
        return Task.FromResult(new NotificationListResponse(
            hasMore ? rows.Take(pageSize).ToList() : rows, unread, page, pageSize, hasMore));
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(_repository.Items.Count(n => n.UserId == userId && !n.IsRead));
}

internal sealed class FakeDeviceTokenRepository : IDeviceTokenRepository
{
    public List<DeviceToken> Items { get; } = new();

    public Task<DeviceToken?> GetByTokenAsync(string token, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(t => t.Token == token));

    public Task<IReadOnlyList<DeviceToken>> ListByUserIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DeviceToken>>(Items.Where(t => userIds.Contains(t.UserId)).ToList());

    public Task AddAsync(DeviceToken token, CancellationToken cancellationToken)
    {
        Items.Add(token);
        return Task.CompletedTask;
    }

    public void Remove(DeviceToken token) => Items.Remove(token);

    public Task<int> RemoveByTokensAsync(IReadOnlyCollection<string> tokens, CancellationToken cancellationToken) =>
        Task.FromResult(Items.RemoveAll(t => tokens.Contains(t.Token)));
}

internal sealed class FakePushSender : IPushSender
{
    public List<(PushMessage Message, IReadOnlyList<PushDevice> Devices)> Sent { get; } = new();

    public bool Delivered { get; set; } = true;

    public bool Throws { get; set; }

    public List<string> InvalidTokens { get; } = new();

    public Task<PushResult> SendAsync(PushMessage message, IReadOnlyList<PushDevice> devices, CancellationToken cancellationToken)
    {
        if (Throws)
        {
            throw new InvalidOperationException("provider down");
        }

        Sent.Add((message, devices));
        return Task.FromResult(new PushResult(Delivered, InvalidTokens.ToList()));
    }
}
