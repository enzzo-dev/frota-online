using FrotaOnline.Application.Payments.Dtos;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Application.Notifications;

namespace FrotaOnline.Tests.Payments;

public class PixAndWebhookTests : PaymentTestBase
{
    private static readonly CreatePixRequest Payer = new(CustomerCpf);

    // ---- Pix of the freight, split between driver and platform ----

    [Fact]
    public async Task Pix_SplitsThePriceBetweenDriverAndPlatform()
    {
        var (_, driver, deliveryId) = await DeliverAsync();

        var result = await PixService.CreateForDeliveryAsync(CustomerId, deliveryId, Payer, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(150m, result.Value.Amount);
        Assert.Equal(145.10m, result.Value.DriverReceives);
        Assert.Equal(4.90m, result.Value.PlatformFee);
        Assert.StartsWith("000201pix", result.Value.PixPayload);

        var charge = Assert.Single(Gateway.PixCharges);
        Assert.Equal(150m, charge.Value);
        var split = Assert.Single(charge.Splits);
        Assert.Equal(driver.PayoutWalletId, split.WalletId);
        Assert.Equal(145.10m, split.FixedValue);
        Assert.Equal(deliveryId.ToString(), charge.ExternalReference);

        var payment = Assert.Single(Payments.Items);
        Assert.False(payment.PaidOutsideApp);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public async Task Pix_SendsThePayersTaxIdToTheProvider_WithoutPunctuation()
    {
        var (_, _, deliveryId) = await DeliverAsync();

        await PixService.CreateForDeliveryAsync(CustomerId, deliveryId, new CreatePixRequest("529.982.247-25"), CancellationToken.None);

        Assert.Equal(CustomerCpf, Assert.Single(Gateway.Customers).TaxId);
    }

    [Fact]
    public async Task Pix_AskedAgainWhileValid_ReturnsTheSameCode()
    {
        var (_, _, deliveryId) = await DeliverAsync();
        var first = await PixService.CreateForDeliveryAsync(CustomerId, deliveryId, Payer, CancellationToken.None);

        var second = await PixService.CreateForDeliveryAsync(CustomerId, deliveryId, Payer, CancellationToken.None);

        Assert.Equal(first.Value.PixPayload, second.Value.PixPayload);
        Assert.Single(Gateway.PixCharges);
        Assert.Single(Payments.Items);
    }

    [Fact]
    public async Task Pix_AfterItExpired_IssuesANewOneOnTheSamePayment()
    {
        var (_, _, deliveryId) = await DeliverAsync();
        var first = await PixService.CreateForDeliveryAsync(CustomerId, deliveryId, Payer, CancellationToken.None);
        Clock.Advance(TimeSpan.FromDays(2));
        Gateway.PixExpiresAt = Clock.UtcNow.AddDays(1);

        var second = await PixService.CreateForDeliveryAsync(CustomerId, deliveryId, Payer, CancellationToken.None);

        Assert.NotEqual(first.Value.PixPayload, second.Value.PixPayload);
        Assert.Single(Payments.Items);
        Assert.Single(Gateway.CancelledCharges);
        Assert.Equal(second.Value.PixPayload, Payments.Items[0].PixPayload);
    }

    [Fact]
    public async Task Pix_WithoutPayoutAccount_ExplainsWhy()
    {
        var (_, _, deliveryId) = await DeliverAsync(linkWallet: false);

        var result = await PixService.CreateForDeliveryAsync(CustomerId, deliveryId, Payer, CancellationToken.None);

        Assert.Equal("payments.driver_without_payout_account", result.Error!.Code);
        Assert.Empty(Gateway.PixCharges);
    }

    [Fact]
    public async Task Pix_BeforeTheDeliveryIsCompleted_IsRejected()
    {
        AddCustomerUser();
        var (_, _, deliveryId, _) = await ScheduleDeliveryAsync();

        var result = await PixService.CreateForDeliveryAsync(CustomerId, deliveryId, Payer, CancellationToken.None);

        Assert.Equal("payment.not_delivered", result.Error!.Code);
    }

    [Fact]
    public async Task Pix_ForSomeoneElsesDelivery_ReturnsNotFound()
    {
        var (_, _, deliveryId) = await DeliverAsync();

        var result = await PixService.CreateForDeliveryAsync(Guid.NewGuid(), deliveryId, Payer, CancellationToken.None);

        Assert.Equal("payments.delivery_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task Pix_WithInvalidTaxId_IsRejectedBeforeCallingTheProvider()
    {
        var (_, _, deliveryId) = await DeliverAsync();

        var result = await PixService.CreateForDeliveryAsync(CustomerId, deliveryId, new CreatePixRequest("123"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(Gateway.Customers);
    }

    [Fact]
    public async Task Pix_AfterPaymentConfirmedOutsideTheApp_IsRejected()
    {
        var (userId, _, deliveryId) = await DeliverAsync();
        await PaymentService.ConfirmAsync(
            userId, deliveryId, new FrotaOnline.Application.Deliveries.Dtos.ConfirmPaymentRequest(PaymentMethod.Cash, null), CancellationToken.None);

        var result = await PixService.CreateForDeliveryAsync(CustomerId, deliveryId, Payer, CancellationToken.None);

        Assert.Equal("payment.already_confirmed", result.Error!.Code);
    }

    [Fact]
    public async Task Pix_WhenTheProviderFails_ReturnsUnavailable_AndKeepsNoPayment()
    {
        var (_, _, deliveryId) = await DeliverAsync();
        Gateway.Fails = true;

        var result = await PixService.CreateForDeliveryAsync(CustomerId, deliveryId, Payer, CancellationToken.None);

        Assert.Equal("payments.unavailable", result.Error!.Code);
        Assert.Empty(Payments.Items);
    }

    // ---- webhook: freight Pix paid ----

    [Fact]
    public async Task Webhook_PaidFreightPix_MarksPaid_SettlesTheFee_AndNotifiesBoth()
    {
        var (driverUser, driver, deliveryId) = await DeliverAsync();
        await PixService.CreateForDeliveryAsync(CustomerId, deliveryId, Payer, CancellationToken.None);
        var gatewayId = Payments.Items.Single().GatewayPaymentId!;
        Assert.Equal(4.90m, await Fees.GetBalanceAsync(driver.Id, CancellationToken.None));
        SentNotifications.Items.Clear();

        await Webhooks.HandleAsync(Paid("evt_1", gatewayId), CancellationToken.None);

        Assert.Equal(PaymentStatus.Paid, Payments.Items.Single().Status);
        Assert.Equal(0m, await Fees.GetBalanceAsync(driver.Id, CancellationToken.None));
        Assert.Contains(SentNotifications.Items, n => n.UserId == CustomerId && n.Type == NotificationType.PaymentConfirmed);
        var received = Assert.Single(SentNotifications.Items, n => n.UserId == driverUser);
        Assert.Equal(NotificationType.PaymentReceived, received.Type);
        Assert.Contains("145,10", received.Body);
    }

    [Fact]
    public async Task Webhook_SameEventTwice_ChangesNothingTheSecondTime()
    {
        var (_, driver, deliveryId) = await DeliverAsync();
        await PixService.CreateForDeliveryAsync(CustomerId, deliveryId, Payer, CancellationToken.None);
        var gatewayId = Payments.Items.Single().GatewayPaymentId!;

        await Webhooks.HandleAsync(Paid("evt_1", gatewayId), CancellationToken.None);
        var ledger = Fees.Items.Count;
        var notices = SentNotifications.Items.Count;
        await Webhooks.HandleAsync(Paid("evt_1", gatewayId), CancellationToken.None);

        Assert.Equal(ledger, Fees.Items.Count);
        Assert.Equal(notices, SentNotifications.Items.Count);
        Assert.Equal(0m, await Fees.GetBalanceAsync(driver.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Webhook_ConfirmedThenReceivedForTheSameCharge_CountsTheMoneyOnce()
    {
        var (_, driver, deliveryId) = await DeliverAsync();
        await PixService.CreateForDeliveryAsync(CustomerId, deliveryId, Payer, CancellationToken.None);
        var gatewayId = Payments.Items.Single().GatewayPaymentId!;

        await Webhooks.HandleAsync(Paid("evt_1", gatewayId, @event: "PAYMENT_CONFIRMED"), CancellationToken.None);
        await Webhooks.HandleAsync(Paid("evt_2", gatewayId, @event: "PAYMENT_RECEIVED"), CancellationToken.None);

        Assert.Equal(0m, await Fees.GetBalanceAsync(driver.Id, CancellationToken.None));
        Assert.Single(Fees.Items, e => e.EntryType == FeeEntryType.FeeSettled);
    }

    [Fact]
    public async Task Webhook_UnknownEventsAndPayments_AreIgnoredButRecorded()
    {
        await Webhooks.HandleAsync(Paid("evt_1", "pay_unknown"), CancellationToken.None);
        await Webhooks.HandleAsync(new WebhookNotification("evt_2", "PAYMENT_CREATED", null, null), CancellationToken.None);
        await Webhooks.HandleAsync(new WebhookNotification("", "PAYMENT_RECEIVED", null, null), CancellationToken.None);

        Assert.Empty(SentNotifications.Items);
        Assert.Contains(WebhookEvents.Items, e => e.EventId == "evt_2");
    }

    // ---- webhook: subscription ----

    [Fact]
    public async Task Webhook_PaidSubscriptionCharge_ActivatesThePlan_AndTellsTheDriver()
    {
        var (userId, _, _) = AddApprovedDriver();
        await SubscriptionService.SubscribeAsync(userId, new SubscribeRequest(SubscriptionPlan.Basic), CancellationToken.None);
        var subscription = Subscriptions.Items.Single();

        await Webhooks.HandleAsync(Paid("evt_1", "pay_first", subscription.GatewaySubscriptionId), CancellationToken.None);

        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.True(subscription.HasAccess(Clock.UtcNow));
        Assert.Equal(Clock.UtcNow.AddMonths(1), subscription.EndsAt);
        Assert.Single(SentNotifications.Items, n => n.UserId == userId && n.Type == NotificationType.SubscriptionActivated);
    }

    [Fact]
    public async Task Webhook_NextMonthsCharge_ExtendsThePlanByAnotherMonth()
    {
        var (userId, _, _) = AddApprovedDriver();
        await SubscriptionService.SubscribeAsync(userId, new SubscribeRequest(SubscriptionPlan.Basic), CancellationToken.None);
        var subscription = Subscriptions.Items.Single();
        await Webhooks.HandleAsync(Paid("evt_1", "pay_1", subscription.GatewaySubscriptionId), CancellationToken.None);

        Clock.Advance(TimeSpan.FromDays(30));
        await Webhooks.HandleAsync(Paid("evt_2", "pay_2", subscription.GatewaySubscriptionId), CancellationToken.None);

        Assert.Equal(Clock.UtcNow.AddDays(-30).AddMonths(2), subscription.EndsAt);
    }

    [Fact]
    public async Task Webhook_SubscriptionDeleted_CancelsButKeepsThePaidPeriod()
    {
        var (userId, _, _) = AddApprovedDriver();
        await SubscriptionService.SubscribeAsync(userId, new SubscribeRequest(SubscriptionPlan.Basic), CancellationToken.None);
        var subscription = Subscriptions.Items.Single();
        await Webhooks.HandleAsync(Paid("evt_1", "pay_1", subscription.GatewaySubscriptionId), CancellationToken.None);

        await Webhooks.HandleAsync(
            new WebhookNotification("evt_2", "SUBSCRIPTION_DELETED", null, subscription.GatewaySubscriptionId), CancellationToken.None);

        Assert.Equal(SubscriptionStatus.Cancelled, subscription.Status);
        Assert.True(subscription.HasAccess(Clock.UtcNow));
    }

    // ---- fee Pix ----

    private async Task<(Guid UserId, DriverProfile Driver)> DriverOwingAsync(int freights)
    {
        var (userId, profile, _) = AddApprovedDriver();
        for (var i = 0; i < freights; i++)
        {
            await Fees.AddAsync(FeeLedger.Accrue(profile.Id, Guid.NewGuid(), 4.90m), CancellationToken.None);
        }

        return (userId, profile);
    }

    [Fact]
    public async Task FeeCharge_IssuesAPixForTheWholeBalance_WithoutSplit()
    {
        var (userId, driver) = await DriverOwingAsync(3);

        var result = await FeeChargeService.CreateAsync(userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(14.70m, result.Value.Amount);
        Assert.Empty(Assert.Single(Gateway.PixCharges).Splits);
        Assert.Equal(14.70m, Gateway.PixCharges[0].Value);
        Assert.Equal(driver.PaymentCustomerId, Gateway.PixCharges[0].CustomerId);
    }

    [Fact]
    public async Task FeeCharge_BelowTheProvidersMinimum_WaitsForMoreFreights()
    {
        var (userId, _) = await DriverOwingAsync(1);

        var result = await FeeChargeService.CreateAsync(userId, CancellationToken.None);

        Assert.Equal("fee.below_minimum", result.Error!.Code);
        Assert.Empty(Gateway.PixCharges);
    }

    [Fact]
    public async Task FeeCharge_WithNothingOwed_ReturnsConflict()
    {
        var (userId, _) = await DriverOwingAsync(0);

        var result = await FeeChargeService.CreateAsync(userId, CancellationToken.None);

        Assert.Equal("fee.nothing_to_charge", result.Error!.Code);
    }

    [Fact]
    public async Task FeeCharge_AskedAgain_ReusesThePixWhileTheBalanceIsTheSame_AndReplacesItWhenItChanges()
    {
        var (userId, driver) = await DriverOwingAsync(2);
        var first = await FeeChargeService.CreateAsync(userId, CancellationToken.None);
        var again = await FeeChargeService.CreateAsync(userId, CancellationToken.None);
        await Fees.AddAsync(FeeLedger.Accrue(driver.Id, Guid.NewGuid(), 4.90m), CancellationToken.None);

        var changed = await FeeChargeService.CreateAsync(userId, CancellationToken.None);

        Assert.Equal(first.Value.PixPayload, again.Value.PixPayload);
        Assert.Equal(14.70m, changed.Value.Amount);
        Assert.Equal(2, Gateway.PixCharges.Count);
        Assert.Single(Gateway.CancelledCharges);
        Assert.Equal(FeeChargeStatus.Cancelled, FeeCharges.Items[0].Status);
    }

    [Fact]
    public async Task Webhook_PaidFeePix_SettlesTheLedgerByItself()
    {
        var (userId, driver) = await DriverOwingAsync(3);
        await FeeChargeService.CreateAsync(userId, CancellationToken.None);
        SentNotifications.Items.Clear();

        await Webhooks.HandleAsync(Paid("evt_1", FeeCharges.Items.Single().GatewayPaymentId), CancellationToken.None);

        Assert.Equal(0m, await Fees.GetBalanceAsync(driver.Id, CancellationToken.None));
        Assert.Equal(FeeChargeStatus.Paid, FeeCharges.Items.Single().Status);
        var notice = Assert.Single(SentNotifications.Items);
        Assert.Equal(NotificationType.FeeSettled, notice.Type);
    }

    [Fact]
    public async Task Webhook_FeePixPaidAfterAnAdminSettledPart_NeverSettlesMoreThanIsOwed()
    {
        var (userId, driver) = await DriverOwingAsync(3);
        await FeeChargeService.CreateAsync(userId, CancellationToken.None);
        await Fees.AddAsync(FeeLedger.Settle(driver.Id, null, 9.80m, "manual"), CancellationToken.None);

        await Webhooks.HandleAsync(Paid("evt_1", FeeCharges.Items.Single().GatewayPaymentId), CancellationToken.None);

        Assert.Equal(0m, await Fees.GetBalanceAsync(driver.Id, CancellationToken.None));
    }
}
