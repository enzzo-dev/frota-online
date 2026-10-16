using FrotaOnline.Application.Common;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Tests.Marketplace;

namespace FrotaOnline.Tests.Routing;

public class RouteServiceTests : MarketplaceTestBase
{
    private (FrotaOnline.Domain.Entities.Address A, FrotaOnline.Domain.Entities.Address B) TwoMappedAddresses() => (
        AddAddress(CustomerId, "Sao Paulo", "Centro", "01001000", -23.55052, -46.633308),
        AddAddress(CustomerId, "Campinas", "Cambui", "13010000", -22.905821, -47.060833));

    [Fact]
    public async Task Preview_ReturnsPinsInOrder_WithDistanceDurationAndLine()
    {
        var (a, b) = TwoMappedAddresses();

        var result = await RouteService.PreviewAsync(
            CustomerId, new(new[] { a.Id, b.Id }), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(12_500, result.Value.DistanceMeters);
        Assert.Equal(1_800, result.Value.DurationSeconds);
        Assert.Equal(new[] { "Sao Paulo", "Campinas" }, result.Value.Waypoints.Select(w => w.Label).ToArray());
        Assert.Equal(new[] { 1, 2 }, result.Value.Waypoints.Select(w => w.Sequence).ToArray());
        Assert.NotEmpty(result.Value.Path);
        Assert.All(result.Value.Waypoints, w => Assert.Null(w.Type));
    }

    [Fact]
    public async Task Preview_WithoutCoordinates_ListsTheAddressesThatNeedAPin()
    {
        var mapped = AddAddress(CustomerId, "Sao Paulo", latitude: -23.5, longitude: -46.6);
        var unmapped = AddAddress(CustomerId, "Campinas", district: "Cambui", zip: "13010000");

        var result = await RouteService.PreviewAsync(
            CustomerId, new(new[] { mapped.Id, unmapped.Id }), CancellationToken.None);

        Assert.Equal("route.missing_coordinates", result.Error!.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains("Campinas", result.Error.Message);
        Assert.DoesNotContain("Sao Paulo", result.Error.Message);
        Assert.Equal(0, Routing.Calls);
    }

    [Fact]
    public async Task Preview_WithAnAddressOfAnotherCustomer_ReturnsNotFound()
    {
        var (a, _) = TwoMappedAddresses();
        var foreign = AddAddress(Guid.NewGuid(), "Santos", "Gonzaga", "11060000", -23.96, -46.33);

        var result = await RouteService.PreviewAsync(
            CustomerId, new(new[] { a.Id, foreign.Id }), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Preview_WithOneOrRepeatedAddress_ReturnsValidation()
    {
        var (a, _) = TwoMappedAddresses();

        var one = await RouteService.PreviewAsync(CustomerId, new(new[] { a.Id }), CancellationToken.None);
        var repeated = await RouteService.PreviewAsync(CustomerId, new(new[] { a.Id, a.Id }), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, one.Error!.Type);
        Assert.Equal(ErrorType.Validation, repeated.Error!.Type);
    }

    [Fact]
    public async Task Preview_WhenTheEngineIsDown_StillReturnsThePins()
    {
        var (a, b) = TwoMappedAddresses();
        Routing.Available = false;

        var result = await RouteService.PreviewAsync(CustomerId, new(new[] { a.Id, b.Id }), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.DistanceMeters);
        Assert.Null(result.Value.DurationSeconds);
        Assert.Empty(result.Value.Path);
        Assert.Equal(2, result.Value.Waypoints.Count);
    }

    [Fact]
    public async Task Preview_SendsLatitudeAndLongitudeToTheEngineInOrder()
    {
        var (a, b) = TwoMappedAddresses();

        await RouteService.PreviewAsync(CustomerId, new(new[] { b.Id, a.Id }), CancellationToken.None);

        Assert.Equal(-22.905821, Routing.LastPoints[0].Latitude);
        Assert.Equal(-46.633308, Routing.LastPoints[1].Longitude);
    }

    [Fact]
    public async Task CreatingAFreight_SavesTheDistanceAndTime()
    {
        var (a, b) = TwoMappedAddresses();

        var created = await FreightService.CreateAsync(
            CustomerId, NewFreight(a, b), CancellationToken.None);

        Assert.Equal(12_500, created.Value.RouteDistanceMeters);
        Assert.Equal(1_800, created.Value.RouteDurationSeconds);
        Assert.Equal(12_500, Freights.Items.Single().RouteDistanceMeters);
    }

    [Fact]
    public async Task CreatingAFreight_WhenTheEngineIsDown_StillWorks()
    {
        var (a, b) = TwoMappedAddresses();
        Routing.Available = false;

        var created = await FreightService.CreateAsync(CustomerId, NewFreight(a, b), CancellationToken.None);

        Assert.True(created.IsSuccess);
        Assert.Null(created.Value.RouteDistanceMeters);
    }

    [Fact]
    public async Task CreatingAFreight_WithAddressesWithoutCoordinates_StillWorks_WithoutARoute()
    {
        var freightId = await CreateFreightAsync();

        Assert.Null(Freights.Items.Single(f => f.Id == freightId).RouteDistanceMeters);
        Assert.Equal(0, Routing.Calls);
    }

    [Fact]
    public async Task FreightRoute_ShowsTheStopTypes_AndHidesOtherCustomersFreights()
    {
        var (a, b) = TwoMappedAddresses();
        var created = await FreightService.CreateAsync(CustomerId, NewFreight(a, b), CancellationToken.None);

        var mine = await RouteService.ForFreightAsync(CustomerId, created.Value.Id, CancellationToken.None);
        var other = await RouteService.ForFreightAsync(Guid.NewGuid(), created.Value.Id, CancellationToken.None);

        Assert.Equal(new StopType?[] { StopType.Pickup, StopType.Delivery }, mine.Value.Waypoints.Select(w => w.Type).ToArray());
        Assert.Equal(ErrorType.NotFound, other.Error!.Type);
    }

    [Fact]
    public async Task DriverBeforeWinning_SeesApproximatePinsAndTheDistance_NeverTheExactPosition()
    {
        var (a, b) = TwoMappedAddresses();
        await FreightService.CreateAsync(CustomerId, NewFreight(a, b), CancellationToken.None);
        var (userId, _, _) = AddApprovedDriver();

        var open = await DriverMarketplace.ListOpenFreightsAsync(userId, CancellationToken.None);

        var freight = Assert.Single(open.Value);
        Assert.Equal(12_500, freight.RouteDistanceMeters);
        Assert.Equal(1_800, freight.RouteDurationSeconds);
        var first = freight.Stops[0];
        Assert.Equal(-23.55, first.ApproxLatitude);
        Assert.Equal(-46.63, first.ApproxLongitude);
        Assert.NotEqual(a.Latitude, first.ApproxLatitude);
        Assert.NotEqual(a.Longitude, first.ApproxLongitude);
    }

    [Fact]
    public async Task DriverAfterWinning_GetsTheExactRoute_AndOnlyForHisOwnDelivery()
    {
        var (a, b) = TwoMappedAddresses();
        var created = await FreightService.CreateAsync(CustomerId, NewFreight(a, b), CancellationToken.None);
        var (userId, _, vehicle) = AddApprovedDriver();
        var proposal = await DriverMarketplace.SubmitProposalAsync(userId, created.Value.Id, Proposal(vehicle), CancellationToken.None);
        var approved = await Selection.ApproveAsync(CustomerId, created.Value.Id, proposal.Value.Id, CancellationToken.None);
        var (otherUser, _, _) = AddApprovedDriver("ZZZ9Z99", name: "Outro");

        var mine = await RouteService.ForDriverDeliveryAsync(userId, approved.Value.DeliveryId, CancellationToken.None);
        var other = await RouteService.ForDriverDeliveryAsync(otherUser, approved.Value.DeliveryId, CancellationToken.None);

        Assert.Equal(a.Latitude, mine.Value.Waypoints[0].Latitude);
        Assert.Equal(12_500, mine.Value.DistanceMeters);
        Assert.Equal(ErrorType.NotFound, other.Error!.Type);
    }
}
