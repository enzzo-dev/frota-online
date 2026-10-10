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
        Assert.Equal("Sao Paulo", result.Value.PickupAddress.City);
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
