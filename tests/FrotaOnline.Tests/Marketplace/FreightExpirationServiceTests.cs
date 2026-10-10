using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Tests.Marketplace;

public class FreightExpirationServiceTests : MarketplaceTestBase
{
    [Fact]
    public async Task ExpireDue_BeforeTheWindowEnds_ChangesNothing()
    {
        var freightId = await CreateFreightAsync();
        var (userId, _, vehicle) = AddApprovedDriver();
        await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(vehicle), CancellationToken.None);
        Clock.Advance(TimeSpan.FromMinutes(4));

        var (freights, proposals) = await Expiration.ExpireDueAsync(CancellationToken.None);

        Assert.Equal((0, 0), (freights, proposals));
        Assert.Equal(FreightStatus.Open, Freights.Items.Single().Status);
    }

    [Fact]
    public async Task ExpireDue_AfterTheWindow_ExpiresTheFreightAndItsPendingProposals()
    {
        var freightId = await CreateFreightAsync();
        var (userId, _, vehicle) = AddApprovedDriver();
        await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(vehicle), CancellationToken.None);
        Clock.Advance(TimeSpan.FromMinutes(5));

        var (freights, proposals) = await Expiration.ExpireDueAsync(CancellationToken.None);

        Assert.Equal((1, 1), (freights, proposals));
        Assert.Equal(FreightStatus.Expired, Freights.Items.Single().Status);
        Assert.Equal(ProposalStatus.Expired, Proposals.Items.Single().Status);
    }

    [Fact]
    public async Task ExpireDue_DoesNotTouchApprovedFreights()
    {
        var freightId = await CreateFreightAsync();
        var (userId, _, vehicle) = AddApprovedDriver();
        var proposal = await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(vehicle), CancellationToken.None);
        await Selection.ApproveAsync(CustomerId, freightId, proposal.Value.Id, CancellationToken.None);
        Clock.Advance(TimeSpan.FromHours(1));

        var (freights, proposals) = await Expiration.ExpireDueAsync(CancellationToken.None);

        Assert.Equal((0, 0), (freights, proposals));
        Assert.Equal(FreightStatus.Approved, Freights.Items.Single().Status);
        Assert.Equal(ProposalStatus.Accepted, Proposals.Items.Single().Status);
    }
}
