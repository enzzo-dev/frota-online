using FrotaOnline.Application.Deliveries;
using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Tests.Marketplace;

public class DeliveryContactPhoneTests : MarketplaceTestBase
{
    public DeliveryContactPhoneTests()
    {
        var customer = User.Create("Maria Souza", "cliente@example.com", "+5511888888888", "hash", UserRole.Customer);
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(customer, CustomerId);
        Users.Items.Add(customer);
    }

    [Fact]
    public async Task AfterApproval_DriverSeesTheCustomerPhone_AndCustomerSeesTheDriverPhone()
    {
        var (driverUserId, _, deliveryId, _) = await ScheduleDeliveryAsync();

        var driverView = await DriverDelivery.GetAsync(driverUserId, deliveryId, CancellationToken.None);
        var list = await DriverDelivery.ListAsync(driverUserId, CancellationToken.None);
        var customerList = await CustomerDeliveries.ListAsync(CustomerId, CancellationToken.None);
        var customerDetail = await CustomerDeliveries.GetAsync(CustomerId, deliveryId, CancellationToken.None);

        Assert.Equal("+5511888888888", driverView.Value.CustomerPhone);
        Assert.Equal("+5511888888888", Assert.Single(list.Value).CustomerPhone);
        Assert.Equal("+5511999999999", Assert.Single(customerList.Value).DriverPhone);
        Assert.Equal("+5511999999999", customerDetail.Value.DriverPhone);
    }

    [Fact]
    public async Task CancelledDelivery_HidesBothPhones()
    {
        var (driverUserId, _, deliveryId, _) = await ScheduleDeliveryAsync();
        await Progress.CancelAsDriverAsync(driverUserId, deliveryId, new FrotaOnline.Application.Deliveries.Dtos.CancelDeliveryRequest("Imprevisto"), CancellationToken.None);

        var driverView = await DriverDelivery.GetAsync(driverUserId, deliveryId, CancellationToken.None);
        var customerDetail = await CustomerDeliveries.GetAsync(CustomerId, deliveryId, CancellationToken.None);

        Assert.Null(driverView.Value.CustomerPhone);
        Assert.Null(customerDetail.Value.DriverPhone);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    public void ContactPhone_IgnoresMissingOrAnonymizedNumbers(string? phone)
    {
        Assert.Null(ContactPhone.For(DeliveryStatus.Scheduled, phone));
    }

    [Fact]
    public async Task OpenFreightAndProposals_NeverCarryAPhone()
    {
        await CreateFreightAsync();
        var (userId, _, _) = AddApprovedDriver();

        var open = await DriverMarketplace.ListOpenFreightsAsync(userId, CancellationToken.None);

        var json = System.Text.Json.JsonSerializer.Serialize(open.Value);
        Assert.DoesNotContain("Phone", json);
    }
}
