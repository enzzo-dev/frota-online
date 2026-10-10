using FrotaOnline.Application.Common;
using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Tests.Marketplace;

public class DriverDeliveryServiceTests : MarketplaceTestBase
{
    private async Task<(Guid DriverUserId, Guid DeliveryId, Guid FreightId)> WinAFourStopFreightAsync()
    {
        var a = AddAddress(CustomerId, "Sao Paulo");
        var b = AddAddress(CustomerId, "Osasco", "Centro", "06010000");
        var c = AddAddress(CustomerId, "Campinas", "Cambui", "13010000");
        var d = AddAddress(CustomerId, "Santos", "Gonzaga", "11050000");
        var stops = new[]
        {
            Stop(StopType.Pickup, a),
            Stop(StopType.Pickup, b),
            Stop(StopType.Delivery, c, items: new[] { ("Caixas", 5) }),
            Stop(StopType.Delivery, d, "Final delivery", ("Pecas", 2))
        };
        var freight = await FreightService.CreateAsync(CustomerId, NewFreight(stops), CancellationToken.None);
        var (userId, _, vehicle) = AddApprovedDriver();
        var proposal = await DriverMarketplace.SubmitProposalAsync(userId, freight.Value.Id, Proposal(vehicle), CancellationToken.None);
        var approved = await Selection.ApproveAsync(CustomerId, freight.Value.Id, proposal.Value.Id, CancellationToken.None);
        return (userId, approved.Value.DeliveryId, freight.Value.Id);
    }

    [Fact]
    public async Task Approve_ReservesTheAgendaByTheNumberOfStops()
    {
        await WinAFourStopFreightAsync();

        var slot = Assert.Single(Agenda.Items);
        Assert.Equal(Clock.UtcNow + DeliveryPlanning.AgendaWindow(4), slot.EndsAt);
        Assert.Equal(TimeSpan.FromHours(6), slot.EndsAt - slot.StartsAt);
    }

    [Fact]
    public async Task GetRoute_Full_ReturnsEveryStopInOrderWithFullAddresses()
    {
        var (userId, deliveryId, _) = await WinAFourStopFreightAsync();

        var result = await DriverDelivery.GetRouteAsync(userId, deliveryId, RouteView.Full, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value.TotalStops);
        Assert.Equal(0, result.Value.CompletedStops);
        Assert.Equal(new[] { StopType.Pickup, StopType.Pickup, StopType.Delivery, StopType.Delivery }, result.Value.Stops.Select(s => s.Type));
        Assert.Equal("Rua Santos", result.Value.Stops[^1].Address.Street);
        Assert.Equal("Final delivery", result.Value.Stops[^1].Note);
        Assert.Equal("Caixas", Assert.Single(result.Value.Stops[2].Items).Description);
    }

    [Fact]
    public async Task GetRoute_Next_ReturnsOnlyTheFirstStopNotCompleted()
    {
        var (userId, deliveryId, freightId) = await WinAFourStopFreightAsync();

        var first = await DriverDelivery.GetRouteAsync(userId, deliveryId, RouteView.Next, CancellationToken.None);
        Freights.Items.Single(f => f.Id == freightId).Stops.OrderBy(s => s.Sequence).First().Complete(Clock.UtcNow);
        var second = await DriverDelivery.GetRouteAsync(userId, deliveryId, RouteView.Next, CancellationToken.None);

        Assert.Equal(1, Assert.Single(first.Value.Stops).Sequence);
        Assert.Equal(2, Assert.Single(second.Value.Stops).Sequence);
        Assert.Equal(1, second.Value.CompletedStops);
        Assert.Equal(4, second.Value.TotalStops);
    }

    [Fact]
    public async Task GetRoute_OfADeliveryWonByAnotherDriver_ReturnsNotFound()
    {
        var (_, deliveryId, _) = await WinAFourStopFreightAsync();
        var (otherUserId, _, _) = AddApprovedDriver("ZZZ9Z99", name: "Other Driver");

        var result = await DriverDelivery.GetRouteAsync(otherUserId, deliveryId, RouteView.Full, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task List_ShowsTheDriversDeliveriesWithStopProgress()
    {
        var (userId, deliveryId, _) = await WinAFourStopFreightAsync();

        var result = await DriverDelivery.ListAsync(userId, CancellationToken.None);

        var row = Assert.Single(result.Value);
        Assert.Equal(deliveryId, row.DeliveryId);
        Assert.Equal(4, row.TotalStops);
        Assert.Equal(DeliveryStatus.Scheduled, row.Status);
    }
}
