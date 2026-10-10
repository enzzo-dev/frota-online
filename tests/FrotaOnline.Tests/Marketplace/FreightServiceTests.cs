using FrotaOnline.Application.Common;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Tests.Marketplace;

public class FreightServiceTests : MarketplaceTestBase
{
    [Fact]
    public async Task Create_SameDay_OpensA5MinuteWindow()
    {
        var (pickup, delivery) = AddAddresses(CustomerId);

        var result = await FreightService.CreateAsync(CustomerId, NewFreight(pickup, delivery), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(FreightStatus.Open, result.Value.Status);
        Assert.Equal(Clock.UtcNow.AddMinutes(5), result.Value.ProposalDeadline);
        Assert.Equal("Sao Paulo", result.Value.Stops[0].Address.City);
        Assert.Equal(1, result.Value.PickupCount);
        Assert.Equal(1, result.Value.DeliveryCount);
    }

    [Fact]
    public async Task Create_Scheduled_OpensA24HourWindow()
    {
        var (pickup, delivery) = AddAddresses(CustomerId);
        var scheduled = DateTime.SpecifyKind(Clock.UtcNow.AddDays(3), DateTimeKind.Utc);

        var result = await FreightService.CreateAsync(CustomerId, NewFreight(pickup, delivery, scheduled: scheduled), CancellationToken.None);

        Assert.Equal(Clock.UtcNow.AddHours(24), result.Value.ProposalDeadline);
        Assert.Equal(scheduled, result.Value.ScheduledPickupAt);
    }

    [Fact]
    public async Task Create_WithoutAcceptedTerms_ReturnsForbidden()
    {
        var (pickup, delivery) = AddAddresses(CustomerId);
        Terms.Accepted = false;

        var result = await FreightService.CreateAsync(CustomerId, NewFreight(pickup, delivery), CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        Assert.Empty(Freights.Items);
    }

    [Fact]
    public async Task Create_WithAddressOfAnotherUser_ReturnsNotFound()
    {
        var (pickup, delivery) = AddAddresses(Guid.NewGuid());

        var result = await FreightService.CreateAsync(CustomerId, NewFreight(pickup, delivery), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Create_WithSameAddressTwice_IsRejectedByValidation()
    {
        var (pickup, _) = AddAddresses(CustomerId);

        var result = await FreightService.CreateAsync(CustomerId, NewFreight(pickup, pickup), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Create_WithSeveralPickupsAndDeliveries_KeepsTheOrderItemsAndFinalAddress()
    {
        var a = AddAddress(CustomerId, "Sao Paulo");
        var b = AddAddress(CustomerId, "Osasco", "Centro", "06010000");
        var c = AddAddress(CustomerId, "Campinas", "Cambui", "13010000");
        var d = AddAddress(CustomerId, "Santos", "Gonzaga", "11050000");
        var stops = new[]
        {
            Stop(StopType.Pickup, a, "Gate 2"),
            Stop(StopType.Pickup, b, items: new[] { ("Pecas", 4) }),
            Stop(StopType.Delivery, c, items: new[] { ("Pecas A", 2), ("Pecas B", 3) }),
            Stop(StopType.Delivery, d, "Final delivery", ("Pecas C", 1))
        };

        var result = await FreightService.CreateAsync(CustomerId, NewFreight(stops), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.PickupCount);
        Assert.Equal(2, result.Value.DeliveryCount);
        Assert.Equal(new[] { 1, 2, 3, 4 }, result.Value.Stops.Select(x => x.Sequence));
        Assert.Equal(new[] { "Sao Paulo", "Osasco", "Campinas", "Santos" }, result.Value.Stops.Select(x => x.Address.City));
        Assert.Equal(2, result.Value.Stops[2].Items.Count);
        Assert.Equal("Santos", result.Value.Stops[^1].Address.City);
        Assert.Empty(result.Value.Stops[0].Items);
        Assert.Equal(4, Freights.Items.Single().Stops.Count);
    }

    [Fact]
    public async Task Create_WhenTheFirstStopIsNotAPickup_IsRejected()
    {
        var (pickup, delivery) = AddAddresses(CustomerId);
        var stops = new[] { Stop(StopType.Delivery, delivery), Stop(StopType.Pickup, pickup) };

        var result = await FreightService.CreateAsync(CustomerId, NewFreight(stops), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Empty(Freights.Items);
    }

    [Fact]
    public async Task Create_WithOnlyOneStop_IsRejected()
    {
        var (pickup, _) = AddAddresses(CustomerId);

        var result = await FreightService.CreateAsync(
            CustomerId, NewFreight(new[] { Stop(StopType.Pickup, pickup) }), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Create_WithAnInvalidItemQuantity_IsRejected()
    {
        var (pickup, delivery) = AddAddresses(CustomerId);
        var stops = new[] { Stop(StopType.Pickup, pickup), Stop(StopType.Delivery, delivery, items: new[] { ("Pecas", 0) }) };

        var result = await FreightService.CreateAsync(CustomerId, NewFreight(stops), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Create_ScheduledInThePast_IsRejected()
    {
        var (pickup, delivery) = AddAddresses(CustomerId);
        var past = DateTime.SpecifyKind(Clock.UtcNow.AddHours(-1), DateTimeKind.Utc);

        var result = await FreightService.CreateAsync(CustomerId, NewFreight(pickup, delivery, scheduled: past), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains("ScheduledPickupAt", result.Error.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Get_FreightOfAnotherCustomer_ReturnsNotFound()
    {
        var freightId = await CreateFreightAsync();

        var result = await FreightService.GetAsync(Guid.NewGuid(), freightId, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task List_FiltersByStatus_AndCountsPendingProposals()
    {
        var freightId = await CreateFreightAsync();
        var (driverUserId, _, vehicle) = AddApprovedDriver();
        await DriverMarketplace.SubmitProposalAsync(driverUserId, freightId, Proposal(vehicle), CancellationToken.None);

        var open = await FreightService.ListAsync(CustomerId, FreightStatus.Open, CancellationToken.None);
        var cancelled = await FreightService.ListAsync(CustomerId, FreightStatus.Cancelled, CancellationToken.None);

        Assert.Equal(1, Assert.Single(open.Value).PendingProposals);
        Assert.Empty(cancelled.Value);
    }

    [Fact]
    public async Task Cancel_ClosesTheFreight_AndRejectsPendingProposals()
    {
        var freightId = await CreateFreightAsync();
        var (driverUserId, _, vehicle) = AddApprovedDriver();
        await DriverMarketplace.SubmitProposalAsync(driverUserId, freightId, Proposal(vehicle), CancellationToken.None);

        var result = await FreightService.CancelAsync(CustomerId, freightId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(FreightStatus.Cancelled, Freights.Items.Single().Status);
        Assert.Equal(ProposalStatus.Rejected, Proposals.Items.Single().Status);
    }

    [Fact]
    public async Task Cancel_Twice_ReturnsConflict()
    {
        var freightId = await CreateFreightAsync();
        await FreightService.CancelAsync(CustomerId, freightId, CancellationToken.None);

        var result = await FreightService.CancelAsync(CustomerId, freightId, CancellationToken.None);

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
    }
}
