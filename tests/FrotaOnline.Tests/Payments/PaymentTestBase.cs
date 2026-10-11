using System.Reflection;
using FrotaOnline.Application.Payments;
using FrotaOnline.Application.Payments.Dtos;
using FrotaOnline.Application.Payments.Validators;
using FrotaOnline.Application.Deliveries.Dtos;
using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Tests.Marketplace;

namespace FrotaOnline.Tests.Payments;

public abstract class PaymentTestBase : MarketplaceTestBase
{
    protected const string CustomerCpf = "52998224725";

    private static readonly CompleteStopRequest Plain = new(null, null, null, null);

    private protected readonly FakeFeeChargeRepository FeeCharges = new();
    private protected readonly FakeWebhookEventRepository WebhookEvents = new();

    private protected SubscriptionService SubscriptionService => new(
        Drivers, Users, Vehicles, Subscriptions, Gateway, UnitOfWork, Clock, new SubscribeRequestValidator());

    private protected PayoutAccountService PayoutService => new(Drivers, UnitOfWork, new UpdatePayoutAccountRequestValidator());

    private protected PixPaymentService PixService => new(
        Deliveries, Payments, Drivers, Users, Gateway, UnitOfWork, Clock, new CreatePixRequestValidator());

    private protected FeeChargeService FeeChargeService => new(
        Drivers, Users, Fees, FeeCharges, Gateway, UnitOfWork, Clock);

    private protected PaymentWebhookService Webhooks => new(
        WebhookEvents, Subscriptions, Payments, FeeCharges, Fees, Deliveries, Drivers, Publisher, UnitOfWork, Clock);

    /// <summary>The customer needs a user row (name, email) to be created at the provider; the base only has an id.</summary>
    protected void AddCustomerUser()
    {
        var user = User.Create("Maria Cliente", "maria@example.com", "+5511988887777", "hash", UserRole.Customer);
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(user, CustomerId);
        Users.Items.Add(user);
    }

    /// <summary>A delivered freight: price R$ 150,00, fee R$ 4,90 accrued in the ledger.</summary>
    private protected async Task<(Guid DriverUser, DriverProfile Driver, Guid DeliveryId)> DeliverAsync(bool linkWallet = true)
    {
        AddCustomerUser();
        var (userId, profile, deliveryId, stops) = await ScheduleDeliveryAsync();
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);
        foreach (var stop in stops)
        {
            await Progress.CompleteStopAsync(userId, deliveryId, stop, Plain, CancellationToken.None);
        }

        if (linkWallet)
        {
            profile.SetPayoutWallet("7b2d6e4a-1c3f-4a5b-9d8e-0f1a2b3c4d5e");
        }

        return (userId, profile, deliveryId);
    }

    protected static WebhookNotification Paid(string eventId, string paymentId, string? subscriptionId = null, string @event = "PAYMENT_RECEIVED") =>
        new(eventId, @event, new WebhookPayment(paymentId, subscriptionId, null, 0m), null);
}
