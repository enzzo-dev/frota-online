using FrotaOnline.Application.Payments.Dtos;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Tests.Payments;

public class SubscriptionTests : PaymentTestBase
{
    private static readonly SubscribeRequest Basic = new(SubscriptionPlan.Basic);

    [Fact]
    public async Task Subscribe_CreatesTheChargeAndReturnsThePaymentPage()
    {
        var (userId, profile, _) = AddApprovedDriver();

        var result = await SubscriptionService.SubscribeAsync(userId, Basic, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionStatus.Pending, result.Value.Status);
        Assert.False(result.Value.HasAccess);
        Assert.Equal(79.90m, result.Value.MonthlyPrice);
        Assert.StartsWith("https://pay.example/", result.Value.PaymentUrl);
        var (customerId, value, reference) = Assert.Single(Gateway.Subscriptions);
        Assert.Equal(79.90m, value);
        Assert.Equal(Subscriptions.Items.Single().Id.ToString(), reference);
        Assert.Equal(customerId, profile.PaymentCustomerId);
    }

    [Fact]
    public async Task Subscribe_Twice_ForTheSamePlan_ReusesTheSamePaymentPage()
    {
        var (userId, _, _) = AddApprovedDriver();
        var first = await SubscriptionService.SubscribeAsync(userId, Basic, CancellationToken.None);

        var second = await SubscriptionService.SubscribeAsync(userId, Basic, CancellationToken.None);

        Assert.Equal(first.Value.PaymentUrl, second.Value.PaymentUrl);
        Assert.Single(Gateway.Subscriptions);
        Assert.Single(Gateway.Customers);
    }

    [Fact]
    public async Task Subscribe_ToAnotherPlanBeforePaying_CancelsTheUnpaidOne()
    {
        var (userId, _, _) = AddApprovedDriver();
        await SubscriptionService.SubscribeAsync(userId, Basic, CancellationToken.None);

        var result = await SubscriptionService.SubscribeAsync(userId, new SubscribeRequest(SubscriptionPlan.Full), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(99.90m, result.Value.MonthlyPrice);
        Assert.Single(Gateway.CancelledSubscriptions);
        Assert.Equal(SubscriptionStatus.Cancelled, Subscriptions.Items[0].Status);
    }

    [Fact]
    public async Task Subscribe_WithMoreThanThreeVehicles_RejectsTheBasicPlan()
    {
        var (userId, profile, _) = AddApprovedDriver("AAA1A11");
        foreach (var plate in new[] { "BBB2B22", "CCC3C33", "DDD4D44" })
        {
            Vehicles.Items.Add(Vehicle.Create(profile.Id, VehicleType.Fiorino, plate, "Fiat", 2020, 600));
        }

        var basic = await SubscriptionService.SubscribeAsync(userId, Basic, CancellationToken.None);
        var full = await SubscriptionService.SubscribeAsync(userId, new SubscribeRequest(SubscriptionPlan.Full), CancellationToken.None);

        Assert.Equal("subscription.too_many_vehicles", basic.Error!.Code);
        Assert.True(full.IsSuccess);
    }

    [Fact]
    public async Task Subscribe_WithoutProvider_ReturnsNotConfigured()
    {
        var (userId, _, _) = AddApprovedDriver();
        Gateway.IsConfigured = false;

        var result = await SubscriptionService.SubscribeAsync(userId, Basic, CancellationToken.None);

        Assert.Equal("payments.not_configured", result.Error!.Code);
    }

    [Fact]
    public async Task Subscribe_WhenTheProviderFails_ReturnsUnavailable_AndSavesNothing()
    {
        var (userId, _, _) = AddApprovedDriver();
        Gateway.Fails = true;

        var result = await SubscriptionService.SubscribeAsync(userId, Basic, CancellationToken.None);

        Assert.Equal("payments.unavailable", result.Error!.Code);
        Assert.Empty(Subscriptions.Items);
    }

    [Fact]
    public async Task Subscribe_WhileActive_ReturnsConflict()
    {
        var (userId, _, _) = AddApprovedDriver();
        var created = await SubscriptionService.SubscribeAsync(userId, Basic, CancellationToken.None);
        Subscriptions.Items.Single().RegisterPayment(Clock.UtcNow);

        var result = await SubscriptionService.SubscribeAsync(userId, Basic, CancellationToken.None);

        Assert.True(created.IsSuccess);
        Assert.Equal("subscription.already_active", result.Error!.Code);
    }

    [Fact]
    public async Task Subscribe_ByADriverNotApproved_IsRejected()
    {
        var (userId, profile, _) = AddApprovedDriver();
        profile.Anonymize();

        var result = await SubscriptionService.SubscribeAsync(userId, Basic, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(Gateway.Subscriptions);
    }

    [Fact]
    public async Task Cancel_KeepsAccessUntilThePaidPeriodEnds()
    {
        var (userId, _, _) = AddApprovedDriver();
        await SubscriptionService.SubscribeAsync(userId, Basic, CancellationToken.None);
        var subscription = Subscriptions.Items.Single();
        subscription.RegisterPayment(Clock.UtcNow);
        var paidUntil = subscription.EndsAt;

        var result = await SubscriptionService.CancelAsync(userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionStatus.Cancelled, result.Value.Status);
        Assert.True(result.Value.HasAccess);
        Assert.Equal(paidUntil, result.Value.EndsAt);
        Assert.Equal(subscription.GatewaySubscriptionId, Assert.Single(Gateway.CancelledSubscriptions));
    }

    [Fact]
    public async Task Cancel_WithoutSubscription_ReturnsNotFound()
    {
        var (userId, _, _) = AddApprovedDriver();

        var result = await SubscriptionService.CancelAsync(userId, CancellationToken.None);

        Assert.Equal("subscription.not_found", result.Error!.Code);
    }

    [Fact]
    public async Task Get_ListsThePlans_AndTheCurrentSubscription()
    {
        var (userId, _, _) = AddApprovedDriver();

        var before = await SubscriptionService.GetAsync(userId, CancellationToken.None);
        await SubscriptionService.SubscribeAsync(userId, Basic, CancellationToken.None);
        var after = await SubscriptionService.GetAsync(userId, CancellationToken.None);

        Assert.Equal(2, before.Value.Plans.Count);
        Assert.Null(before.Value.Current);
        Assert.Equal(3, before.Value.Plans[0].MaxVehicles);
        Assert.Null(before.Value.Plans[1].MaxVehicles);
        Assert.NotNull(after.Value.Current);
    }

    [Fact]
    public void RegisterPayment_EarlyPayment_ExtendsFromTheEndOfThePeriod()
    {
        var subscription = Subscription.Create(Guid.NewGuid(), SubscriptionPlan.Basic, "+5511999999999");
        var start = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        subscription.RegisterPayment(start);
        subscription.RegisterPayment(start.AddDays(20));

        Assert.Equal(start.AddMonths(2), subscription.EndsAt);
        Assert.True(subscription.HasAccess(start.AddDays(40)));
        Assert.False(subscription.HasAccess(start.AddMonths(2)));
    }

    [Fact]
    public void RegisterPayment_AfterItLapsed_CountsFromThePayment()
    {
        var subscription = Subscription.Create(Guid.NewGuid(), SubscriptionPlan.Basic, "+5511999999999");
        var start = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        subscription.RegisterPayment(start);

        var late = start.AddMonths(3);
        subscription.RegisterPayment(late);

        Assert.Equal(late.AddMonths(1), subscription.EndsAt);
    }

    // ---- limits enforced elsewhere ----

    [Fact]
    public async Task BasicPlan_BlocksAFourthVehicle_ButFullDoesNot()
    {
        var (_, profile, _) = AddApprovedDriver("AAA1A11");
        foreach (var plate in new[] { "BBB2B22", "CCC3C33" })
        {
            Vehicles.Items.Add(Vehicle.Create(profile.Id, VehicleType.Fiorino, plate, "Fiat", 2020, 600));
        }

        var basic = Subscription.Create(profile.Id, SubscriptionPlan.Basic, "+5511999999999");
        basic.RegisterPayment(Clock.UtcNow);
        Subscriptions.Items.Add(basic);

        Assert.True((await SubscriptionPolicy.EnsureCanAddVehicleAsync(profile.Id, 2, CancellationToken.None)).IsSuccess);
        var blocked = await SubscriptionPolicy.EnsureCanAddVehicleAsync(profile.Id, 3, CancellationToken.None);
        Assert.Equal("subscription.vehicle_limit", blocked.Error!.Code);

        Subscriptions.Items.Clear();
        var full = Subscription.Create(profile.Id, SubscriptionPlan.Full, "+5511999999999");
        full.RegisterPayment(Clock.UtcNow);
        Subscriptions.Items.Add(full);
        Assert.True((await SubscriptionPolicy.EnsureCanAddVehicleAsync(profile.Id, 10, CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task Proposals_AreOnlyBlockedWhenTheSubscriptionIsRequired()
    {
        var freightId = await CreateFreightAsync();
        var (userId, profile, vehicle) = AddApprovedDriver();

        var open = await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(vehicle), CancellationToken.None);
        SubscriptionRules.RequiredToPropose = true;
        var otherFreight = await CreateFreightAsync();
        var blocked = await DriverMarketplace.SubmitProposalAsync(userId, otherFreight, Proposal(vehicle), CancellationToken.None);

        var paid = Subscription.Create(profile.Id, SubscriptionPlan.Basic, "+5511999999999");
        paid.RegisterPayment(Clock.UtcNow);
        Subscriptions.Items.Add(paid);
        var allowed = await DriverMarketplace.SubmitProposalAsync(userId, otherFreight, Proposal(vehicle), CancellationToken.None);

        Assert.True(open.IsSuccess);
        Assert.Equal("subscription.required", blocked.Error!.Code);
        Assert.True(allowed.IsSuccess);
    }

    [Fact]
    public async Task PayoutAccount_AcceptsAWalletId_AndRejectsGarbage()
    {
        var (userId, profile, _) = AddApprovedDriver();

        var bad = await PayoutService.UpdateAsync(userId, new UpdatePayoutAccountRequest("not-a-uuid"), CancellationToken.None);
        var good = await PayoutService.UpdateAsync(
            userId, new UpdatePayoutAccountRequest("7b2d6e4a-1c3f-4a5b-9d8e-0f1a2b3c4d5e"), CancellationToken.None);
        var read = await PayoutService.GetAsync(userId, CancellationToken.None);

        Assert.True(bad.IsFailure);
        Assert.True(good.Value.Configured);
        Assert.Equal(profile.PayoutWalletId, read.Value.WalletId);
    }
}
