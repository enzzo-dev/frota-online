using FrotaOnline.Application.Common;
using FrotaOnline.Application.Deliveries.Dtos;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Tests.Marketplace;

namespace FrotaOnline.Tests.Deliveries;

public class DeliveryPaymentAndFeeTests : MarketplaceTestBase
{
    private static readonly CompleteStopRequest Plain = new(null, null, null, null);

    private async Task<(Guid UserId, Guid ProfileId, Guid DeliveryId)> DeliverAsync()
    {
        var (userId, profile, deliveryId, stops) = await ScheduleDeliveryAsync();
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);
        foreach (var stop in stops)
        {
            await Progress.CompleteStopAsync(userId, deliveryId, stop, Plain, CancellationToken.None);
        }

        return (userId, profile.Id, deliveryId);
    }

    [Fact]
    public async Task ConfirmPayment_AfterDelivery_RecordsAPaidPaymentOutsideTheApp()
    {
        var (userId, _, deliveryId) = await DeliverAsync();

        var result = await PaymentService.ConfirmAsync(
            userId, deliveryId, new ConfirmPaymentRequest(PaymentMethod.Pix, " E123 "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Paid, result.Value.Status);
        Assert.Equal(150m, result.Value.Amount);
        var payment = Assert.Single(Payments.Items);
        Assert.True(payment.PaidOutsideApp);
        Assert.Equal("E123", payment.ExternalReference);
    }

    [Fact]
    public async Task ConfirmPayment_BeforeDelivery_ReturnsConflict()
    {
        var (userId, _, deliveryId, _) = await ScheduleDeliveryAsync();

        var result = await PaymentService.ConfirmAsync(
            userId, deliveryId, new ConfirmPaymentRequest(PaymentMethod.Cash, null), CancellationToken.None);

        Assert.Equal("payment.not_delivered", result.Error!.Code);
    }

    [Fact]
    public async Task ConfirmPayment_Twice_ReturnsConflict()
    {
        var (userId, _, deliveryId) = await DeliverAsync();
        await PaymentService.ConfirmAsync(userId, deliveryId, new ConfirmPaymentRequest(PaymentMethod.Cash, null), CancellationToken.None);

        var result = await PaymentService.ConfirmAsync(userId, deliveryId, new ConfirmPaymentRequest(PaymentMethod.Cash, null), CancellationToken.None);

        Assert.Equal("payment.already_confirmed", result.Error!.Code);
        Assert.Single(Payments.Items);
    }

    [Fact]
    public async Task ConfirmPayment_ByAnotherDriver_ReturnsNotFound()
    {
        var (_, _, deliveryId) = await DeliverAsync();
        var (other, _, _) = AddApprovedDriver("ZZZ9Z99", name: "Outro");

        var result = await PaymentService.ConfirmAsync(other, deliveryId, new ConfirmPaymentRequest(PaymentMethod.Cash, null), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Statement_AfterOneDelivery_OwesTheFlatFee()
    {
        var (userId, _, _) = await DeliverAsync();

        var result = await FeeService.StatementForDriverAsync(userId, CancellationToken.None);

        Assert.Equal(4.90m, result.Value.Balance);
        Assert.Equal(4.90m, result.Value.TotalAccrued);
        Assert.Equal(0m, result.Value.TotalSettled);
        Assert.Single(result.Value.Entries);
    }

    [Fact]
    public async Task Settle_PartOfTheBalance_LowersIt()
    {
        var (_, profileId, _) = await DeliverAsync();

        var result = await FeeService.SettleAsync(profileId, new SettleFeeRequest(2m, "Pix received"), CancellationToken.None);

        Assert.Equal(2.90m, result.Value.Balance);
        Assert.Equal(2m, result.Value.TotalSettled);
    }

    [Fact]
    public async Task Settle_MoreThanTheBalance_ReturnsConflict()
    {
        var (_, profileId, _) = await DeliverAsync();

        var result = await FeeService.SettleAsync(profileId, new SettleFeeRequest(10m, "Too much"), CancellationToken.None);

        Assert.Equal("fee.exceeds_balance", result.Error!.Code);
        Assert.Single(Fees.Items);
    }

    [Fact]
    public async Task Settle_WithZeroOrNoDescription_ReturnsValidation()
    {
        var (_, profileId, _) = await DeliverAsync();

        var zero = await FeeService.SettleAsync(profileId, new SettleFeeRequest(0m, "x"), CancellationToken.None);
        var blank = await FeeService.SettleAsync(profileId, new SettleFeeRequest(1m, " "), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, zero.Error!.Type);
        Assert.Equal(ErrorType.Validation, blank.Error!.Type);
    }

    [Fact]
    public async Task Settle_UnknownDriver_ReturnsNotFound()
    {
        var result = await FeeService.SettleAsync(Guid.NewGuid(), new SettleFeeRequest(1m, "x"), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task CustomerView_ShowsProgress_Payment_AndTimeline()
    {
        var (userId, _, deliveryId) = await DeliverAsync();
        await PaymentService.ConfirmAsync(userId, deliveryId, new ConfirmPaymentRequest(PaymentMethod.Pix, null), CancellationToken.None);

        var list = await CustomerDeliveries.ListAsync(CustomerId, CancellationToken.None);
        var detail = await CustomerDeliveries.GetAsync(CustomerId, deliveryId, CancellationToken.None);

        var row = Assert.Single(list.Value);
        Assert.Equal(DeliveryStatus.Delivered, row.Status);
        Assert.Equal(2, row.CompletedStops);
        Assert.Equal(PaymentStatus.Paid, row.Payment);
        Assert.Equal("Joao Silva", row.DriverName);
        Assert.Equal(DeliveryStatus.Delivered, detail.Value.Timeline[^1].Status);
        Assert.All(detail.Value.Stops, s => Assert.NotNull(s.CompletedAt));
    }

    [Fact]
    public async Task CustomerView_OfAnotherCustomer_ReturnsNotFound()
    {
        var (_, _, deliveryId) = await DeliverAsync();

        var list = await CustomerDeliveries.ListAsync(Guid.NewGuid(), CancellationToken.None);
        var detail = await CustomerDeliveries.GetAsync(Guid.NewGuid(), deliveryId, CancellationToken.None);

        Assert.Empty(list.Value);
        Assert.Equal(ErrorType.NotFound, detail.Error!.Type);
    }
}
