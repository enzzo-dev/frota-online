using FrotaOnline.Application.Common;
using FrotaOnline.Application.Deliveries.Dtos;
using FrotaOnline.Tests.Marketplace;

namespace FrotaOnline.Tests.Deliveries;

public class DeliveryLocationServiceTests : MarketplaceTestBase
{
    private static readonly UpdateLocationRequest Here = new(-23.55, -46.63, 90, 40);

    [Fact]
    public async Task Update_BeforeTheDriverLeaves_ReturnsConflict()
    {
        var (userId, _, deliveryId, _) = await ScheduleDeliveryAsync();

        var result = await LocationService.UpdateAsync(userId, deliveryId, Here, CancellationToken.None);

        Assert.Equal("delivery.invalid_status", result.Error!.Code);
        Assert.Empty(Locations.Items);
    }

    [Fact]
    public async Task Update_WhileUnderWay_KeepsASingleRowThatFollowsTheDriver()
    {
        var (userId, _, deliveryId, _) = await ScheduleDeliveryAsync();
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);

        await LocationService.UpdateAsync(userId, deliveryId, Here, CancellationToken.None);
        var second = await LocationService.UpdateAsync(userId, deliveryId, Here with { Latitude = -23.56 }, CancellationToken.None);

        Assert.True(second.IsSuccess);
        var row = Assert.Single(Locations.Items);
        Assert.Equal(-23.56, row.Latitude);
        Assert.Equal(2, Locations.FastPathHits);
    }

    [Fact]
    public async Task Cancelling_TheDelivery_ForgetsTheLastPosition()
    {
        var (userId, _, deliveryId, _) = await ScheduleDeliveryAsync();
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);
        await LocationService.UpdateAsync(userId, deliveryId, Here, CancellationToken.None);

        var cancel = await Progress.CancelAsDriverAsync(userId, deliveryId, new CancelDeliveryRequest("Truck broke"), CancellationToken.None);
        var late = await LocationService.UpdateAsync(userId, deliveryId, Here, CancellationToken.None);

        Assert.True(cancel.IsSuccess);
        Assert.Empty(Locations.Items);
        Assert.Equal("delivery.invalid_status", late.Error!.Code);
    }

    [Fact]
    public async Task Update_ByAnotherDriver_ReturnsNotFound()
    {
        var (userId, _, deliveryId, _) = await ScheduleDeliveryAsync();
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);
        var (other, _, _) = AddApprovedDriver("ZZZ9Z99", name: "Outro");

        var result = await LocationService.UpdateAsync(other, deliveryId, Here, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Update_WithInvalidCoordinates_IsRejectedByValidation()
    {
        var (userId, _, deliveryId, _) = await ScheduleDeliveryAsync();
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);

        var result = await LocationService.UpdateAsync(userId, deliveryId, Here with { Latitude = 120 }, CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }
}
