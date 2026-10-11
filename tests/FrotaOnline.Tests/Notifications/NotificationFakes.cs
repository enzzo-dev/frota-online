using FrotaOnline.Application.Abstractions.Notifications;
using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Abstractions.Storage;
using FrotaOnline.Application.Notifications.Dtos;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Tests.Marketplace;
using FrotaOnline.Tests.Onboarding;

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

internal sealed class FakeFileStorage : IFileStorage
{
    public bool IsConfigured { get; set; }

    /// <summary>Objects "uploaded" so far, by key.</summary>
    public Dictionary<string, StoredObject> Objects { get; } = new();

    public List<string> Deleted { get; } = new();

    public Task<UploadTicket> CreateUploadAsync(
        string key, string contentType, long maxBytes, TimeSpan lifetime, CancellationToken cancellationToken) =>
        Task.FromResult(new UploadTicket(
            $"https://storage.test/{key}",
            new Dictionary<string, string> { ["Content-Type"] = contentType },
            new DateTime(2026, 10, 8, 12, 15, 0, DateTimeKind.Utc)));

    public Task<StoredObject?> GetInfoAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(Objects.TryGetValue(key, out var stored) ? stored : null);

    public Task<string> CreateDownloadUrlAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken) =>
        Task.FromResult($"https://storage.test/{key}?signed");

    public Task DeleteAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
    {
        Deleted.AddRange(keys);
        foreach (var key in keys)
        {
            Objects.Remove(key);
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakeNotificationPreferenceRepository : INotificationPreferenceRepository
{
    public List<NotificationPreference> Items { get; } = new();

    public Task<NotificationPreference?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(p => p.UserId == userId));

    public Task AddAsync(NotificationPreference preference, CancellationToken cancellationToken)
    {
        Items.Add(preference);
        return Task.CompletedTask;
    }

    public Task<IReadOnlySet<Guid>> ListPushDisabledAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>>(
            Items.Where(p => !p.PushEnabled && userIds.Contains(p.UserId)).Select(p => p.UserId).ToHashSet());
}

/// <summary>The same rules as the SQL query, over the in-memory fakes.</summary>
internal sealed class FakeMatchingDriverQueries : IMatchingDriverQueries
{
    private readonly FakeDriverRepository _drivers;
    private readonly FakeVehicleRepository _vehicles;
    private readonly FakeDriverAgendaRepository _agenda;
    private readonly FakeNotificationPreferenceRepository _preferences;

    public FakeMatchingDriverQueries(
        FakeDriverRepository drivers, FakeVehicleRepository vehicles,
        FakeDriverAgendaRepository agenda, FakeNotificationPreferenceRepository preferences)
    {
        _drivers = drivers;
        _vehicles = vehicles;
        _agenda = agenda;
        _preferences = preferences;
    }

    public Task<IReadOnlyList<Guid>> ListUserIdsForNewFreightAsync(
        FreightMatchCriteria criteria, int limit, CancellationToken cancellationToken)
    {
        var ids = _drivers.Items
            .Where(d => d.Status == DriverStatus.Approved && d.UserId != criteria.CustomerId)
            .Where(d => _vehicles.Items.Any(v => v.DriverProfileId == d.Id && v.IsActive
                                                 && v.Type == criteria.VehicleType && v.CapacityKg >= criteria.CargoWeightKg))
            .Where(d => !_agenda.Items.Any(s => s.DriverProfileId == d.Id && s.Overlaps(criteria.StartsAt, criteria.EndsAt)))
            .Where(d => !_preferences.Items.Any(p => p.UserId == d.UserId && !p.NewFreightAlerts))
            .Select(d => d.UserId)
            .Take(limit)
            .ToList();
        return Task.FromResult<IReadOnlyList<Guid>>(ids);
    }
}
