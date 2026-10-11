using FrotaOnline.Application.Deliveries.Dtos;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Tests.Marketplace;

namespace FrotaOnline.Tests.Notifications;

/// <summary>Each business event leaves the right notification for the right person.</summary>
public class NotificationTriggerTests : MarketplaceTestBase
{
    private static readonly CompleteStopRequest Plain = new(null, null, null, null);

    private IReadOnlyList<Notification> For(Guid userId) =>
        SentNotifications.Items.Where(n => n.UserId == userId).ToList();

    private IReadOnlyList<Notification> For(Guid userId, NotificationType type) =>
        SentNotifications.Items.Where(n => n.UserId == userId && n.Type == type).ToList();

    [Fact]
    public async Task NewProposal_TellsTheCustomer_WithThePrice()
    {
        var freightId = await CreateFreightAsync();
        var (driverUser, _, vehicle) = AddApprovedDriver();

        await DriverMarketplace.SubmitProposalAsync(driverUser, freightId, Proposal(vehicle, 150m), CancellationToken.None);

        var notification = Assert.Single(For(CustomerId));
        Assert.Equal(NotificationType.ProposalReceived, notification.Type);
        Assert.Equal(NotificationTarget.Freight, notification.TargetType);
        Assert.Equal(freightId, notification.TargetId);
        Assert.Contains("R$ 150,00", notification.Body);
        Assert.Empty(For(driverUser));
    }

    [Fact]
    public async Task RejectedProposal_LeavesNoNotification()
    {
        var freightId = await CreateFreightAsync();
        var (driverUser, _, vehicle) = AddApprovedDriver();

        var result = await DriverMarketplace.SubmitProposalAsync(driverUser, freightId, Proposal(vehicle, -1m), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(SentNotifications.Items);
    }

    [Fact]
    public async Task Approval_TellsTheWinner_AndTheDriversNotChosen()
    {
        var freightId = await CreateFreightAsync();
        var (winnerUser, _, winnerVehicle) = AddApprovedDriver("AAA1A11", name: "Vencedor");
        var (loserUser, _, loserVehicle) = AddApprovedDriver("BBB2B22", name: "Perdedor");
        var win = await DriverMarketplace.SubmitProposalAsync(winnerUser, freightId, Proposal(winnerVehicle, 120m), CancellationToken.None);
        var lose = await DriverMarketplace.SubmitProposalAsync(loserUser, freightId, Proposal(loserVehicle, 180m), CancellationToken.None);

        var approved = await Selection.ApproveAsync(CustomerId, freightId, win.Value.Id, CancellationToken.None);

        var won = Assert.Single(For(winnerUser));
        Assert.Equal(NotificationType.ProposalApproved, won.Type);
        Assert.Equal(approved.Value.DeliveryId, won.TargetId);
        var lost = Assert.Single(For(loserUser));
        Assert.Equal(NotificationType.ProposalNotSelected, lost.Type);
        Assert.Equal(lose.Value.Id, lost.TargetId);
    }

    [Fact]
    public async Task FailedApproval_LeavesNoApprovalNotifications()
    {
        var freightId = await CreateFreightAsync();
        var (driverUser, _, vehicle) = AddApprovedDriver();
        await DriverMarketplace.SubmitProposalAsync(driverUser, freightId, Proposal(vehicle), CancellationToken.None);
        var before = SentNotifications.Items.Count;

        var result = await Selection.ApproveAsync(CustomerId, freightId, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(before, SentNotifications.Items.Count);
    }

    [Fact]
    public async Task DeliveryRun_TellsTheCustomerAtEachMilestone()
    {
        var (userId, _, deliveryId, stops) = await ScheduleDeliveryAsync(deliveryStops: 2);
        SentNotifications.Items.Clear();

        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);
        await Progress.CompleteStopAsync(userId, deliveryId, stops[0], Plain, CancellationToken.None);
        await Progress.CompleteStopAsync(userId, deliveryId, stops[1], Plain, CancellationToken.None);
        await Progress.CompleteStopAsync(userId, deliveryId, stops[2], Plain, CancellationToken.None);

        var types = For(CustomerId).Select(n => n.Type).ToList();
        Assert.Equal(
            new[]
            {
                NotificationType.DeliveryStarted,
                NotificationType.DeliveryPickedUp,
                NotificationType.DeliveryStopCompleted,
                NotificationType.DeliveryCompleted
            },
            types);
        Assert.All(For(CustomerId), n => Assert.Equal(deliveryId, n.TargetId));
        Assert.Contains("2 de 3", For(CustomerId, NotificationType.DeliveryStopCompleted).Single().Body);
        Assert.Empty(For(userId));
    }

    [Fact]
    public async Task SingleDeliveryStop_ProducesPickedUpThenCompleted()
    {
        var (userId, _, deliveryId, stops) = await ScheduleDeliveryAsync();
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);
        SentNotifications.Items.Clear();

        await Progress.CompleteStopAsync(userId, deliveryId, stops[0], Plain, CancellationToken.None);
        await Progress.CompleteStopAsync(userId, deliveryId, stops[1], Plain, CancellationToken.None);

        Assert.Equal(
            new[] { NotificationType.DeliveryPickedUp, NotificationType.DeliveryCompleted },
            For(CustomerId).Select(n => n.Type).ToArray());
    }

    [Fact]
    public async Task Incident_TellsTheCustomer()
    {
        var (userId, _, deliveryId, _) = await ScheduleDeliveryAsync();
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);
        SentNotifications.Items.Clear();

        await Progress.ReportIncidentAsync(userId, deliveryId, new ReportIncidentRequest("Pneu furado"), CancellationToken.None);

        var notification = Assert.Single(For(CustomerId));
        Assert.Equal(NotificationType.DeliveryIncident, notification.Type);
        Assert.Contains("Pneu furado", notification.Body);
    }

    [Fact]
    public async Task DriverCancel_TellsTheCustomer()
    {
        var (userId, _, deliveryId, _) = await ScheduleDeliveryAsync();
        SentNotifications.Items.Clear();

        await Progress.CancelAsDriverAsync(userId, deliveryId, new CancelDeliveryRequest("Caminhão quebrou"), CancellationToken.None);

        var notification = Assert.Single(SentNotifications.Items);
        Assert.Equal(CustomerId, notification.UserId);
        Assert.Equal(NotificationType.DeliveryCancelled, notification.Type);
        Assert.Contains("Caminhão quebrou", notification.Body);
    }

    [Fact]
    public async Task CustomerCancel_TellsTheDriver()
    {
        var (userId, _, deliveryId, _) = await ScheduleDeliveryAsync();
        SentNotifications.Items.Clear();

        await Progress.CancelAsCustomerAsync(CustomerId, deliveryId, new CancelDeliveryRequest("Não preciso mais"), CancellationToken.None);

        var notification = Assert.Single(SentNotifications.Items);
        Assert.Equal(userId, notification.UserId);
        Assert.Equal(NotificationType.DeliveryCancelled, notification.Type);
    }

    [Fact]
    public async Task PaymentConfirmation_TellsTheCustomer()
    {
        var (userId, _, deliveryId, stops) = await ScheduleDeliveryAsync();
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);
        foreach (var stop in stops)
        {
            await Progress.CompleteStopAsync(userId, deliveryId, stop, Plain, CancellationToken.None);
        }

        SentNotifications.Items.Clear();

        await PaymentService.ConfirmAsync(userId, deliveryId, new ConfirmPaymentRequest(PaymentMethod.Pix, null), CancellationToken.None);

        var notification = Assert.Single(SentNotifications.Items);
        Assert.Equal(CustomerId, notification.UserId);
        Assert.Equal(NotificationType.PaymentConfirmed, notification.Type);
        Assert.Contains("R$ 150,00", notification.Body);
    }

    [Fact]
    public async Task FeeSettlement_TellsTheDriver()
    {
        var (userId, profile, deliveryId, stops) = await ScheduleDeliveryAsync();
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);
        foreach (var stop in stops)
        {
            await Progress.CompleteStopAsync(userId, deliveryId, stop, Plain, CancellationToken.None);
        }

        SentNotifications.Items.Clear();

        await FeeService.SettleAsync(profile.Id, new SettleFeeRequest(2m, "Pix recebido"), CancellationToken.None);

        var notification = Assert.Single(SentNotifications.Items);
        Assert.Equal(userId, notification.UserId);
        Assert.Equal(NotificationType.FeeSettled, notification.Type);
    }

    [Fact]
    public async Task Expiration_TellsTheCustomerAboutTheFreight_AndTheDriverAboutTheProposal()
    {
        var freightId = await CreateFreightAsync();
        var (driverUser, _, vehicle) = AddApprovedDriver();
        var proposal = await DriverMarketplace.SubmitProposalAsync(driverUser, freightId, Proposal(vehicle), CancellationToken.None);
        SentNotifications.Items.Clear();

        Clock.Advance(TimeSpan.FromMinutes(6));
        await Expiration.ExpireDueAsync(CancellationToken.None);

        var freightNotice = Assert.Single(For(CustomerId));
        Assert.Equal(NotificationType.FreightExpired, freightNotice.Type);
        Assert.Equal(freightId, freightNotice.TargetId);
        var proposalNotice = Assert.Single(For(driverUser));
        Assert.Equal(NotificationType.ProposalExpired, proposalNotice.Type);
        Assert.Equal(proposal.Value.Id, proposalNotice.TargetId);
    }

    [Fact]
    public async Task Expiration_WithNothingDue_NotifiesNobody()
    {
        await CreateFreightAsync();

        await Expiration.ExpireDueAsync(CancellationToken.None);

        Assert.Empty(SentNotifications.Items);
    }
}
