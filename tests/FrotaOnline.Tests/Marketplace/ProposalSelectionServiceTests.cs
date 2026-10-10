using FrotaOnline.Application.Common;
using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Tests.Marketplace;

public class ProposalSelectionServiceTests : MarketplaceTestBase
{
    [Fact]
    public async Task Ranking_OrdersByPrice_ThenByArrival()
    {
        var freightId = await CreateFreightAsync();
        var (userA, _, vehicleA) = AddApprovedDriver("AAA1A11", name: "Ana");
        var (userB, _, vehicleB) = AddApprovedDriver("BBB2B22", name: "Bruno");
        var (userC, _, vehicleC) = AddApprovedDriver("CCC3C33", name: "Caio");
        await DriverMarketplace.SubmitProposalAsync(userA, freightId, Proposal(vehicleA, 200m), CancellationToken.None);
        await DriverMarketplace.SubmitProposalAsync(userB, freightId, Proposal(vehicleB, 150m), CancellationToken.None);
        await DriverMarketplace.SubmitProposalAsync(userC, freightId, Proposal(vehicleC, 150m), CancellationToken.None);

        var result = await Selection.ListRankedAsync(CustomerId, freightId, CancellationToken.None);

        Assert.Equal(new[] { "Bruno", "Caio", "Ana" }, result.Value.Select(r => r.DriverName).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, result.Value.Select(r => r.Rank).ToArray());
    }

    [Fact]
    public async Task Ranking_OfAnotherCustomersFreight_ReturnsNotFound()
    {
        var freightId = await CreateFreightAsync();

        var result = await Selection.ListRankedAsync(Guid.NewGuid(), freightId, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Approve_CreatesDelivery_ReservesAgenda_AndRejectsTheOthers()
    {
        var freightId = await CreateFreightAsync();
        var (userA, profileA, vehicleA) = AddApprovedDriver("AAA1A11");
        var (userB, _, vehicleB) = AddApprovedDriver("BBB2B22");
        var winner = await DriverMarketplace.SubmitProposalAsync(userA, freightId, Proposal(vehicleA, 140m), CancellationToken.None);
        await DriverMarketplace.SubmitProposalAsync(userB, freightId, Proposal(vehicleB, 160m), CancellationToken.None);

        var result = await Selection.ApproveAsync(CustomerId, freightId, winner.Value.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(140m, result.Value.AgreedPrice);
        Assert.Equal(PlatformFees.PerFreight, result.Value.PlatformFee);
        Assert.Equal(DeliveryStatus.Scheduled, result.Value.Status);
        Assert.Equal(FreightStatus.Approved, Freights.Items.Single().Status);
        Assert.Equal(winner.Value.Id, Freights.Items.Single().ApprovedProposalId);
        Assert.Equal(ProposalStatus.Accepted, Proposals.Items.Single(p => p.Id == winner.Value.Id).Status);
        Assert.Equal(ProposalStatus.Rejected, Proposals.Items.Single(p => p.Id != winner.Value.Id).Status);

        var delivery = Assert.Single(Deliveries.Items);
        Assert.Equal(profileA.Id, delivery.DriverProfileId);
        Assert.Equal(DeliveryStatus.Scheduled, Assert.Single(Deliveries.Events).Status);

        var slot = Assert.Single(Agenda.Items);
        Assert.Equal(delivery.Id, slot.DeliveryId);
        Assert.Equal(Clock.UtcNow, slot.StartsAt);
        Assert.Equal(Clock.UtcNow + DeliveryPlanning.AgendaWindow(2), slot.EndsAt);
    }

    [Fact]
    public async Task Approve_ScheduledFreight_ReservesTheAgendaAtThePickupTime()
    {
        var pickupAt = DateTime.SpecifyKind(Clock.UtcNow.AddDays(2), DateTimeKind.Utc);
        var freightId = await CreateFreightAsync(scheduled: pickupAt);
        var (userId, _, vehicle) = AddApprovedDriver();
        var proposal = await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(vehicle), CancellationToken.None);

        var result = await Selection.ApproveAsync(CustomerId, freightId, proposal.Value.Id, CancellationToken.None);

        Assert.Equal(pickupAt, result.Value.AgendaStartsAt);
    }

    [Fact]
    public async Task Approve_AfterTheWindowEnds_ReturnsConflict()
    {
        var freightId = await CreateFreightAsync();
        var (userId, _, vehicle) = AddApprovedDriver();
        var proposal = await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(vehicle), CancellationToken.None);
        Clock.Advance(TimeSpan.FromMinutes(6));

        var result = await Selection.ApproveAsync(CustomerId, freightId, proposal.Value.Id, CancellationToken.None);

        Assert.Equal("freight.window_closed", result.Error!.Code);
        Assert.Empty(Deliveries.Items);
    }

    [Fact]
    public async Task Approve_ByAnotherCustomer_ReturnsNotFound()
    {
        var freightId = await CreateFreightAsync();
        var (userId, _, vehicle) = AddApprovedDriver();
        var proposal = await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(vehicle), CancellationToken.None);

        var result = await Selection.ApproveAsync(Guid.NewGuid(), freightId, proposal.Value.Id, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Approve_ProposalOfAnotherFreight_ReturnsNotFound()
    {
        var freightId = await CreateFreightAsync();

        var result = await Selection.ApproveAsync(CustomerId, freightId, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("proposal.not_found", result.Error!.Code);
    }

    [Fact]
    public async Task Approve_WithdrawnProposal_ReturnsConflict()
    {
        var freightId = await CreateFreightAsync();
        var (userId, _, vehicle) = AddApprovedDriver();
        var proposal = await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(vehicle), CancellationToken.None);
        await DriverMarketplace.WithdrawProposalAsync(userId, proposal.Value.Id, CancellationToken.None);

        var result = await Selection.ApproveAsync(CustomerId, freightId, proposal.Value.Id, CancellationToken.None);

        Assert.Equal("proposal.not_pending", result.Error!.Code);
    }

    [Fact]
    public async Task Approve_WhenDriverGotBusyInTheMeantime_ReturnsConflict()
    {
        var freightId = await CreateFreightAsync();
        var (userId, profile, vehicle) = AddApprovedDriver();
        var proposal = await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(vehicle), CancellationToken.None);
        Agenda.Items.Add(FrotaOnline.Domain.Entities.DriverAgendaSlot.Create(
            profile.Id, Guid.NewGuid(), Clock.UtcNow, Clock.UtcNow.AddHours(1)));

        var result = await Selection.ApproveAsync(CustomerId, freightId, proposal.Value.Id, CancellationToken.None);

        Assert.Equal("proposal.driver_busy", result.Error!.Code);
        Assert.Equal(FreightStatus.Open, Freights.Items.Single().Status);
    }

    [Fact]
    public async Task Approve_Twice_SecondCallReturnsConflict()
    {
        var freightId = await CreateFreightAsync();
        var (userId, _, vehicle) = AddApprovedDriver();
        var proposal = await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(vehicle), CancellationToken.None);
        await Selection.ApproveAsync(CustomerId, freightId, proposal.Value.Id, CancellationToken.None);

        var result = await Selection.ApproveAsync(CustomerId, freightId, proposal.Value.Id, CancellationToken.None);

        Assert.Equal("freight.not_open", result.Error!.Code);
        Assert.Single(Deliveries.Items);
    }
}
