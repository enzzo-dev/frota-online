using FrotaOnline.Application.Notifications;
using FrotaOnline.Application.Notifications.Dtos;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Tests.Auth;
using FrotaOnline.Tests.Marketplace;

namespace FrotaOnline.Tests.Notifications;

/// <summary>A new freight tells the drivers it fits, unless they turned the alert off.</summary>
public class CompatibleFreightAlertTests : MarketplaceTestBase
{
    private IReadOnlyList<Notification> Alerts(Guid userId) =>
        SentNotifications.Items.Where(n => n.UserId == userId && n.Type == NotificationType.NewFreightAvailable).ToList();

    [Fact]
    public async Task NewFreight_AlertsACompatibleDriver_WithRouteAndWindow()
    {
        var (driverUser, _, _) = AddApprovedDriver();

        var freightId = await CreateFreightAsync();

        var alert = Assert.Single(Alerts(driverUser));
        Assert.Equal(NotificationTarget.Freight, alert.TargetType);
        Assert.Equal(freightId, alert.TargetId);
        Assert.Contains("Sao Paulo", alert.Body);
        Assert.Contains("Campinas", alert.Body);
        Assert.Contains("5 minutos", alert.Body);
    }

    [Fact]
    public async Task NewScheduledFreight_MentionsThe24HourWindow()
    {
        var (driverUser, _, _) = AddApprovedDriver();

        await CreateFreightAsync(scheduled: DateTime.SpecifyKind(Clock.UtcNow.AddDays(2), DateTimeKind.Utc));

        Assert.Contains("24 horas", Assert.Single(Alerts(driverUser)).Body);
    }

    [Fact]
    public async Task NewFreight_SkipsDriversWhoseVehicleDoesNotFit()
    {
        var (wrongType, _, _) = AddApprovedDriver("AAA1A11", VehicleType.Truck);
        var (tooSmall, _, _) = AddApprovedDriver("BBB2B22", VehicleType.Fiorino, capacity: 100);

        await CreateFreightAsync(type: VehicleType.Fiorino, weight: 300);

        Assert.Empty(Alerts(wrongType));
        Assert.Empty(Alerts(tooSmall));
    }

    [Fact]
    public async Task NewFreight_SkipsDriversNotApproved()
    {
        var (driverUser, profile, _) = AddApprovedDriver();
        profile.Anonymize();

        await CreateFreightAsync();

        Assert.Empty(Alerts(driverUser));
    }

    [Fact]
    public async Task NewFreight_SkipsDriversBusyInThatPeriod()
    {
        var (busyUser, busyProfile, _) = AddApprovedDriver("AAA1A11");
        var (freeUser, _, _) = AddApprovedDriver("BBB2B22");
        Agenda.Items.Add(DriverAgendaSlot.Create(busyProfile.Id, Guid.NewGuid(), Clock.UtcNow.AddMinutes(-30), Clock.UtcNow.AddHours(3)));

        await CreateFreightAsync();

        Assert.Empty(Alerts(busyUser));
        Assert.Single(Alerts(freeUser));
    }

    [Fact]
    public async Task NewFreight_SkipsDriversWhoTurnedTheAlertOff()
    {
        var (quietUser, _, _) = AddApprovedDriver("AAA1A11");
        var (otherUser, _, _) = AddApprovedDriver("BBB2B22");
        await PreferenceService.UpdateAsync(quietUser, new UpdateNotificationPreferencesRequest(false, null), CancellationToken.None);

        await CreateFreightAsync();

        Assert.Empty(Alerts(quietUser));
        Assert.Single(Alerts(otherUser));
    }

    [Fact]
    public async Task NewFreight_NeverAlertsTheCustomerWhoPostedIt()
    {
        var (driverUser, _, _) = AddApprovedDriver();
        var (pickup, delivery) = AddAddresses(driverUser);
        Terms.Accepted = true;

        await FreightService.CreateAsync(driverUser, NewFreight(pickup, delivery), CancellationToken.None);

        Assert.Empty(Alerts(driverUser));
    }

    [Fact]
    public async Task FailedFreight_AlertsNobody()
    {
        AddApprovedDriver();
        Terms.Accepted = false;

        var (pickup, delivery) = AddAddresses(CustomerId);

        var result = await FreightService.CreateAsync(CustomerId, NewFreight(pickup, delivery), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Empty(SentNotifications.Items);
    }

    [Fact]
    public async Task Preferences_DefaultToEverythingOn()
    {
        var result = await PreferenceService.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.Value.NewFreightAlerts);
        Assert.True(result.Value.PushEnabled);
        Assert.Empty(Preferences.Items);
    }

    [Fact]
    public async Task Preferences_UpdateChangesOnlyWhatWasSent()
    {
        var user = Guid.NewGuid();

        await PreferenceService.UpdateAsync(user, new UpdateNotificationPreferencesRequest(false, null), CancellationToken.None);
        var second = await PreferenceService.UpdateAsync(user, new UpdateNotificationPreferencesRequest(null, false), CancellationToken.None);

        Assert.False(second.Value.NewFreightAlerts);
        Assert.False(second.Value.PushEnabled);
        Assert.Single(Preferences.Items);

        var third = await PreferenceService.UpdateAsync(user, new UpdateNotificationPreferencesRequest(true, null), CancellationToken.None);
        Assert.True(third.Value.NewFreightAlerts);
        Assert.False(third.Value.PushEnabled);
    }

    [Fact]
    public async Task Dispatch_WithPushOff_SettlesWithoutSending()
    {
        var user = Guid.NewGuid();
        var devices = new FakeDeviceTokenRepository();
        devices.Items.Add(DeviceToken.Create(user, "token-1", DevicePlatform.Android, Clock.UtcNow));
        var push = new FakePushSender();
        await PreferenceService.UpdateAsync(user, new UpdateNotificationPreferencesRequest(null, false), CancellationToken.None);
        var notification = Notification.Create(user, NotificationType.ProposalReceived, "T", "B", NotificationTarget.Freight, Guid.NewGuid());
        SentNotifications.Items.Add(notification);
        var dispatcher = new NotificationDispatcher(SentNotifications, devices, Preferences, push, UnitOfWork, Clock);

        var settled = await dispatcher.DispatchPendingAsync(CancellationToken.None);

        Assert.Equal(1, settled);
        Assert.Empty(push.Sent);
        Assert.NotNull(notification.PushDispatchedAt);
        Assert.False(notification.IsRead);
    }
}
