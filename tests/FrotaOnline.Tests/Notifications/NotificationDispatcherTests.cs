using FrotaOnline.Application.Notifications;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Tests.Auth;

namespace FrotaOnline.Tests.Notifications;

public class NotificationDispatcherTests
{
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeDeviceTokenRepository _devices = new();
    private readonly FakePushSender _push = new();
    private readonly FakeNotificationPreferenceRepository _preferences = new();
    private readonly FakeClock _clock = new();
    private readonly NotificationDispatcher _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public NotificationDispatcherTests()
    {
        _sut = new NotificationDispatcher(_notifications, _devices, _preferences, _push, new FakeUnitOfWork(), _clock);
    }

    private Notification AddNotification(Guid? userId = null)
    {
        var notification = Notification.Create(
            userId ?? _userId, NotificationType.ProposalReceived, "Titulo", "Corpo", NotificationTarget.Freight, Guid.NewGuid());
        _notifications.Items.Add(notification);
        return notification;
    }

    private void AddDevice(string token = "token-1", Guid? userId = null) =>
        _devices.Items.Add(DeviceToken.Create(userId ?? _userId, token, DevicePlatform.Android, _clock.UtcNow));

    [Fact]
    public async Task Dispatch_WithoutDevices_SettlesTheNotificationWithoutPushing()
    {
        var notification = AddNotification();

        var settled = await _sut.DispatchPendingAsync(CancellationToken.None);

        Assert.Equal(1, settled);
        Assert.NotNull(notification.PushDispatchedAt);
        Assert.Empty(_push.Sent);
    }

    [Fact]
    public async Task Dispatch_SendsTitleBodyAndTargetToEveryDeviceOfThePerson()
    {
        var notification = AddNotification();
        AddDevice("token-1");
        AddDevice("token-2");
        AddDevice("token-other", Guid.NewGuid());

        await _sut.DispatchPendingAsync(CancellationToken.None);

        var (message, devices) = Assert.Single(_push.Sent);
        Assert.Equal("Titulo", message.Title);
        Assert.Equal(notification.TargetId.ToString(), message.Data["targetId"]);
        Assert.Equal("Freight", message.Data["targetType"]);
        Assert.Equal(new[] { "token-1", "token-2" }, devices.Select(d => d.Token).OrderBy(t => t));
        Assert.NotNull(notification.PushDispatchedAt);
    }

    [Fact]
    public async Task Dispatch_ProviderFailure_KeepsItPendingAndCountsTheAttempt()
    {
        var notification = AddNotification();
        AddDevice();
        _push.Delivered = false;

        var settled = await _sut.DispatchPendingAsync(CancellationToken.None);

        Assert.Equal(0, settled);
        Assert.Null(notification.PushDispatchedAt);
        Assert.Equal(1, notification.PushAttempts);
    }

    [Fact]
    public async Task Dispatch_ProviderThrowing_IsTreatedAsAFailure()
    {
        var notification = AddNotification();
        AddDevice();
        _push.Throws = true;

        var settled = await _sut.DispatchPendingAsync(CancellationToken.None);

        Assert.Equal(0, settled);
        Assert.Equal(1, notification.PushAttempts);
    }

    [Fact]
    public async Task Dispatch_GivesUpAfterTheMaximumNumberOfAttempts()
    {
        var notification = AddNotification();
        AddDevice();
        _push.Delivered = false;

        for (var i = 0; i < Notification.MaxPushAttempts; i++)
        {
            await _sut.DispatchPendingAsync(CancellationToken.None);
        }

        Assert.NotNull(notification.PushDispatchedAt);
        Assert.Equal(Notification.MaxPushAttempts, notification.PushAttempts);
        Assert.Equal(0, await _sut.DispatchPendingAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Dispatch_RemovesTokensTheProviderRejected()
    {
        AddNotification();
        AddDevice("dead");
        AddDevice("alive");
        _push.InvalidTokens.Add("dead");

        await _sut.DispatchPendingAsync(CancellationToken.None);

        Assert.Equal("alive", Assert.Single(_devices.Items).Token);
    }

    [Fact]
    public async Task Dispatch_SkipsNotificationsAlreadyRead()
    {
        var notification = AddNotification();
        AddDevice();
        notification.MarkRead(_clock.UtcNow);

        await _sut.DispatchPendingAsync(CancellationToken.None);

        Assert.Empty(_push.Sent);
    }

    [Fact]
    public async Task Dispatch_WithNothingPending_DoesNothing()
    {
        Assert.Equal(0, await _sut.DispatchPendingAsync(CancellationToken.None));
    }
}
