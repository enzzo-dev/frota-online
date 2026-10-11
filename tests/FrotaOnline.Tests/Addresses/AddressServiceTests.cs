using FrotaOnline.Application.Addresses.Dtos;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Deliveries.Dtos;
using FrotaOnline.Tests.Marketplace;

namespace FrotaOnline.Tests.Addresses;

public class AddressServiceTests : MarketplaceTestBase
{
    private static CreateAddressRequest Changed(double? latitude = null, double? longitude = null) =>
        new("Deposito", "Rua Nova", "99", "Fundos", "Mooca", "Sao Paulo", "sp", "03101-000", latitude, longitude);

    private async Task<Guid> NewAddressAsync()
    {
        var created = await AddressService.CreateAsync(
            CustomerId, new("Casa", "Rua A", "10", null, "Centro", "Sao Paulo", "SP", "01001000", -23.5, -46.6), CancellationToken.None);
        return created.Value.Id;
    }

    [Fact]
    public async Task Update_AnUnusedAddress_ReplacesEveryField()
    {
        var id = await NewAddressAsync();

        var result = await AddressService.UpdateAsync(CustomerId, id, Changed(-23.55, -46.63), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Rua Nova", result.Value.Street);
        Assert.Equal("SP", result.Value.State);
        Assert.Equal("03101000", result.Value.ZipCode);
        Assert.Equal(-23.55, result.Value.Latitude);
    }

    [Fact]
    public async Task Update_WithoutCoordinates_ClearsThem()
    {
        var id = await NewAddressAsync();

        var result = await AddressService.UpdateAsync(CustomerId, id, Changed(), CancellationToken.None);

        Assert.Null(result.Value.Latitude);
        Assert.Null(result.Value.Longitude);
    }

    [Fact]
    public async Task Update_WithAnInvalidState_ReturnsValidation()
    {
        var id = await NewAddressAsync();

        var result = await AddressService.UpdateAsync(
            CustomerId, id, Changed() with { State = "XX" }, CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Update_AnAddressOfAnotherCustomer_ReturnsNotFound()
    {
        var id = await NewAddressAsync();

        var result = await AddressService.UpdateAsync(Guid.NewGuid(), id, Changed(), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Update_AnAddressUsedByAFreight_ReturnsConflict()
    {
        await CreateFreightAsync();
        var used = Addresses.Items.First();

        var result = await AddressService.UpdateAsync(CustomerId, used.Id, Changed(), CancellationToken.None);

        Assert.Equal("address.in_use", result.Error!.Code);
        Assert.Equal("Rua A", used.Street);
    }

    [Fact]
    public async Task Delete_AnUnusedAddress_RemovesItForGood()
    {
        var id = await NewAddressAsync();

        var result = await AddressService.DeleteAsync(CustomerId, id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(Addresses.Items, a => a.Id == id);
    }

    [Fact]
    public async Task Delete_AnAddressOfAnOpenFreight_ReturnsConflict()
    {
        await CreateFreightAsync();
        var used = Addresses.Items.First();

        var result = await AddressService.DeleteAsync(CustomerId, used.Id, CancellationToken.None);

        Assert.Equal("address.in_active_use", result.Error!.Code);
        Assert.False(used.IsDeleted);
    }

    [Fact]
    public async Task Delete_AnAddressOfADeliveryInProgress_ReturnsConflict()
    {
        await ScheduleDeliveryAsync();
        var used = Addresses.Items.First();

        var result = await AddressService.DeleteAsync(CustomerId, used.Id, CancellationToken.None);

        Assert.Equal("address.in_active_use", result.Error!.Code);
    }

    [Fact]
    public async Task Delete_AfterTheFreightWasCancelled_HidesTheAddressButKeepsTheHistory()
    {
        var freightId = await CreateFreightAsync();
        await FreightService.CancelAsync(CustomerId, freightId, CancellationToken.None);
        var used = Addresses.Items.First();

        var result = await AddressService.DeleteAsync(CustomerId, used.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(Addresses.Items, a => a.Id == used.Id && a.IsDeleted);
        var listed = await AddressService.ListAsync(CustomerId, CancellationToken.None);
        Assert.DoesNotContain(listed.Value, a => a.Id == used.Id);
        // The finished freight still shows where it went.
        var freight = await FreightService.GetAsync(CustomerId, freightId, CancellationToken.None);
        Assert.Equal("Rua A", freight.Value.Stops[0].Address.Street);
    }

    [Fact]
    public async Task Delete_TwiceOrOfAnotherCustomer_ReturnsNotFound()
    {
        var id = await NewAddressAsync();

        var other = await AddressService.DeleteAsync(Guid.NewGuid(), id, CancellationToken.None);
        await AddressService.DeleteAsync(CustomerId, id, CancellationToken.None);
        var again = await AddressService.DeleteAsync(CustomerId, id, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, other.Error!.Type);
        Assert.Equal(ErrorType.NotFound, again.Error!.Type);
    }

    [Fact]
    public async Task ADeletedAddress_CannotBeUsedInANewFreightOrPreview()
    {
        var freightId = await CreateFreightAsync();
        await FreightService.CancelAsync(CustomerId, freightId, CancellationToken.None);
        var (pickup, delivery) = (Addresses.Items[0], Addresses.Items[1]);
        await AddressService.DeleteAsync(CustomerId, pickup.Id, CancellationToken.None);

        var created = await FreightService.CreateAsync(CustomerId, NewFreight(pickup, delivery), CancellationToken.None);
        var preview = await RouteService.PreviewAsync(CustomerId, new(new[] { pickup.Id, delivery.Id }), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, created.Error!.Type);
        Assert.Equal(ErrorType.NotFound, preview.Error!.Type);
    }
}

public class DriverCancelBeforePickupTests : MarketplaceTestBase
{
    [Fact]
    public async Task Driver_CanCancel_WhileScheduled()
    {
        var run = await ScheduleDeliveryAsync();

        var result = await Progress.CancelAsDriverAsync(
            run.UserId, run.DeliveryId, new CancelDeliveryRequest("Truck in the shop"), CancellationToken.None);

        Assert.Equal(FrotaOnline.Domain.Enums.DeliveryStatus.Cancelled, result.Value.Status);
        Assert.False(Agenda.Items.Single().IsActive);
        Assert.Empty(Fees.Items);
    }

    [Fact]
    public async Task Driver_CanCancel_OnTheWayToTheFirstPickup()
    {
        var run = await ScheduleDeliveryAsync();
        await Progress.StartAsync(run.UserId, run.DeliveryId, CancellationToken.None);

        var result = await Progress.CancelAsDriverAsync(
            run.UserId, run.DeliveryId, new CancelDeliveryRequest("Flat tire"), CancellationToken.None);

        Assert.Equal(FrotaOnline.Domain.Enums.DeliveryStatus.Cancelled, result.Value.Status);
        Assert.Contains("driver", Deliveries.Events.Last().Note);
    }
}
