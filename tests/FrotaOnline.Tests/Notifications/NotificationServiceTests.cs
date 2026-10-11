using FrotaOnline.Application.Common;
using FrotaOnline.Application.Notifications;
using FrotaOnline.Application.Notifications.Dtos;
using FrotaOnline.Application.Notifications.Validators;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Tests.Auth;

namespace FrotaOnline.Tests.Notifications;

public class NotificationServiceTests
{
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeDeviceTokenRepository _devices = new();
    private readonly FakeClock _clock = new();
    private readonly NotificationService _sut;
    private readonly DeviceService _deviceService;
    private readonly Guid _userId = Guid.NewGuid();

    public NotificationServiceTests()
    {
        var unitOfWork = new FakeUnitOfWork();
        _sut = new NotificationService(_notifications, new FakeNotificationQueries(_notifications), unitOfWork, _clock);
        _deviceService = new DeviceService(_devices, unitOfWork, _clock, new RegisterDeviceRequestValidator());
    }

    private Notification Add(Guid? userId = null)
    {
        var notification = Notification.Create(
            userId ?? _userId, NotificationType.DeliveryCompleted, "Titulo", "Corpo", NotificationTarget.Delivery, Guid.NewGuid());
        _notifications.Items.Add(notification);
        return notification;
    }

    [Fact]
    public async Task List_ReturnsOnlyMyNotifications_WithTheUnreadCount()
    {
        Add();
        Add().MarkRead(_clock.UtcNow);
        Add(Guid.NewGuid());

        var result = await _sut.ListAsync(_userId, unreadOnly: false, 1, 20, CancellationToken.None);

        Assert.Equal(2, result.Value.Items.Count);
        Assert.Equal(1, result.Value.UnreadCount);
        Assert.False(result.Value.HasMore);
    }

    [Fact]
    public async Task List_UnreadOnly_HidesReadOnes()
    {
        Add();
        Add().MarkRead(_clock.UtcNow);

        var result = await _sut.ListAsync(_userId, unreadOnly: true, 1, 20, CancellationToken.None);

        Assert.Single(result.Value.Items);
    }

    [Fact]
    public async Task List_PagesAndSignalsTheNextPage()
    {
        for (var i = 0; i < 3; i++)
        {
            Add();
        }

        var first = await _sut.ListAsync(_userId, false, 1, 2, CancellationToken.None);
        var second = await _sut.ListAsync(_userId, false, 2, 2, CancellationToken.None);

        Assert.Equal(2, first.Value.Items.Count);
        Assert.True(first.Value.HasMore);
        Assert.Single(second.Value.Items);
        Assert.False(second.Value.HasMore);
    }

    [Fact]
    public async Task List_ClampsAbsurdPagingValues()
    {
        Add();

        var result = await _sut.ListAsync(_userId, false, -5, 100_000, CancellationToken.None);

        Assert.Equal(1, result.Value.Page);
        Assert.Equal(100, result.Value.PageSize);
    }

    [Fact]
    public async Task MarkRead_SetsReadAt_AndKeepsTheFirstTimeOnRepeat()
    {
        var notification = Add();
        await _sut.MarkReadAsync(_userId, notification.Id, CancellationToken.None);
        var firstRead = notification.ReadAt;
        _clock.Advance(TimeSpan.FromHours(1));

        var again = await _sut.MarkReadAsync(_userId, notification.Id, CancellationToken.None);

        Assert.True(again.IsSuccess);
        Assert.NotNull(firstRead);
        Assert.Equal(firstRead, notification.ReadAt);
    }

    [Fact]
    public async Task MarkRead_OfSomeoneElsesNotification_ReturnsNotFound()
    {
        var notification = Add(Guid.NewGuid());

        var result = await _sut.MarkReadAsync(_userId, notification.Id, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task MarkAllRead_ClearsTheBadge_OnlyForMe()
    {
        Add();
        Add();
        var other = Add(Guid.NewGuid());

        await _sut.MarkAllReadAsync(_userId, CancellationToken.None);

        Assert.Equal(0, (await _sut.CountUnreadAsync(_userId, CancellationToken.None)).Value.Unread);
        Assert.False(other.IsRead);
    }

    [Fact]
    public async Task RegisterDevice_StoresTheToken()
    {
        var result = await _deviceService.RegisterAsync(
            _userId, new RegisterDeviceRequest(" abc ", DevicePlatform.Android), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var device = Assert.Single(_devices.Items);
        Assert.Equal("abc", device.Token);
        Assert.Equal(_userId, device.UserId);
    }

    [Fact]
    public async Task RegisterDevice_SameTokenTwice_KeepsOneRow()
    {
        var request = new RegisterDeviceRequest("abc", DevicePlatform.Ios);
        await _deviceService.RegisterAsync(_userId, request, CancellationToken.None);

        await _deviceService.RegisterAsync(_userId, request, CancellationToken.None);

        Assert.Single(_devices.Items);
    }

    [Fact]
    public async Task RegisterDevice_TokenOfAnotherPerson_MovesToTheNewLogin()
    {
        var previous = Guid.NewGuid();
        await _deviceService.RegisterAsync(previous, new RegisterDeviceRequest("abc", DevicePlatform.Android), CancellationToken.None);

        await _deviceService.RegisterAsync(_userId, new RegisterDeviceRequest("abc", DevicePlatform.Android), CancellationToken.None);

        Assert.Equal(_userId, Assert.Single(_devices.Items).UserId);
    }

    [Fact]
    public async Task RegisterDevice_WithoutToken_ReturnsValidationError()
    {
        var result = await _deviceService.RegisterAsync(
            _userId, new RegisterDeviceRequest("", DevicePlatform.Android), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Empty(_devices.Items);
    }

    [Fact]
    public async Task UnregisterDevice_RemovesMyToken_ButNeverSomeoneElses()
    {
        await _deviceService.RegisterAsync(_userId, new RegisterDeviceRequest("mine", DevicePlatform.Android), CancellationToken.None);
        await _deviceService.RegisterAsync(Guid.NewGuid(), new RegisterDeviceRequest("theirs", DevicePlatform.Android), CancellationToken.None);

        var mine = await _deviceService.UnregisterAsync(_userId, "mine", CancellationToken.None);
        var theirs = await _deviceService.UnregisterAsync(_userId, "theirs", CancellationToken.None);

        Assert.True(mine.IsSuccess);
        Assert.True(theirs.IsSuccess);
        Assert.Equal("theirs", Assert.Single(_devices.Items).Token);
    }
}
