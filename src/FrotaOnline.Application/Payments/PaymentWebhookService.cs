using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Payments;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Notifications;
using FrotaOnline.Application.Payments.Dtos;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Payments;

public interface IPaymentWebhookService
{
    /// <summary>Handles one provider notification. Safe to call again with the same event: it does nothing the second time.</summary>
    Task HandleAsync(WebhookNotification notification, CancellationToken cancellationToken);
}

/// <summary>
/// Turns provider events into business changes: a paid subscription charge opens or extends the plan, a paid freight
/// Pix marks the payment paid and settles the platform fee that the split already kept, a paid fee Pix settles the
/// driver's ledger. Everything an event changes is saved in one transaction together with the event record.
/// </summary>
public sealed class PaymentWebhookService : IPaymentWebhookService
{
    private readonly IWebhookEventRepository _events;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IPaymentRepository _payments;
    private readonly IFeeChargeRepository _feeCharges;
    private readonly IFeeLedgerRepository _ledger;
    private readonly IDeliveryRepository _deliveries;
    private readonly IDriverRepository _drivers;
    private readonly INotificationPublisher _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public PaymentWebhookService(
        IWebhookEventRepository events,
        ISubscriptionRepository subscriptions,
        IPaymentRepository payments,
        IFeeChargeRepository feeCharges,
        IFeeLedgerRepository ledger,
        IDeliveryRepository deliveries,
        IDriverRepository drivers,
        INotificationPublisher notifications,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock)
    {
        _events = events;
        _subscriptions = subscriptions;
        _payments = payments;
        _feeCharges = feeCharges;
        _ledger = ledger;
        _deliveries = deliveries;
        _drivers = drivers;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task HandleAsync(WebhookNotification notification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(notification.EventId) || await _events.ExistsAsync(notification.EventId, cancellationToken))
        {
            return;
        }

        switch (notification.Event)
        {
            case "PAYMENT_RECEIVED" or "PAYMENT_CONFIRMED" when notification.Payment is not null:
                await HandlePaidAsync(notification.Payment, cancellationToken);
                break;
            case "SUBSCRIPTION_DELETED" or "SUBSCRIPTION_INACTIVATED" when notification.SubscriptionId is not null:
                await HandleSubscriptionEndedAsync(notification.SubscriptionId, cancellationToken);
                break;
        }

        // Every event is recorded, handled or not, so a repeated delivery is dropped at the first check.
        await _events.AddAsync(WebhookEvent.Create(notification.EventId, notification.Event), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task HandlePaidAsync(WebhookPayment payment, CancellationToken cancellationToken)
    {
        // A card charge sends CONFIRMED and, weeks later, RECEIVED for the same charge: count the money once.
        var paidKey = $"paid:{payment.Id}";
        if (await _events.ExistsAsync(paidKey, cancellationToken))
        {
            return;
        }

        var now = _clock.UtcNow;
        var handled = false;

        if (payment.SubscriptionId is not null)
        {
            handled = await ActivateSubscriptionAsync(payment.SubscriptionId, now, cancellationToken);
        }
        else if (await _payments.GetByGatewayIdAsync(payment.Id, cancellationToken) is { } freightPayment)
        {
            handled = await SettleFreightPaymentAsync(freightPayment, now, cancellationToken);
        }
        else if (await _feeCharges.GetByGatewayIdAsync(payment.Id, cancellationToken) is { } feeCharge)
        {
            handled = await SettleFeeChargeAsync(feeCharge, now, cancellationToken);
        }

        if (handled)
        {
            await _events.AddAsync(WebhookEvent.Create(paidKey, "PAID"), cancellationToken);
        }
    }

    private async Task<bool> ActivateSubscriptionAsync(string gatewaySubscriptionId, DateTime now, CancellationToken cancellationToken)
    {
        var subscription = await _subscriptions.GetByGatewayIdAsync(gatewaySubscriptionId, cancellationToken);
        if (subscription is null)
        {
            return false;
        }

        subscription.RegisterPayment(now);
        var driver = await _drivers.GetByIdAsync(subscription.DriverProfileId, cancellationToken);
        if (driver is not null)
        {
            await _notifications.PublishAsync(
                NotificationMessages.SubscriptionActivated(driver.UserId, subscription.Id, subscription.EndsAt!.Value),
                cancellationToken);
        }

        return true;
    }

    private async Task<bool> SettleFreightPaymentAsync(Payment payment, DateTime now, CancellationToken cancellationToken)
    {
        if (payment.Status == PaymentStatus.Paid)
        {
            return true;
        }

        var delivery = await _deliveries.GetByIdAsync(payment.DeliveryId, cancellationToken);
        if (delivery is null)
        {
            return false;
        }

        payment.MarkPaid(now, payment.GatewayPaymentId);

        // The split already sent the platform its fee, so the fee accrued when the delivery was completed is now paid.
        await _ledger.AddAsync(
            FeeLedger.Settle(delivery.DriverProfileId, delivery.Id, delivery.PlatformFee, "Platform fee kept by the Pix split"),
            cancellationToken);

        var driver = await _drivers.GetByIdAsync(delivery.DriverProfileId, cancellationToken);
        var messages = new List<NotificationMessage>
        {
            NotificationMessages.PaymentConfirmed(delivery.CustomerId, delivery.Id, payment.Amount)
        };
        if (driver is not null)
        {
            messages.Add(NotificationMessages.PaymentReceived(driver.UserId, delivery.Id, payment.Amount - delivery.PlatformFee));
        }

        await _notifications.PublishAsync(messages, cancellationToken);
        return true;
    }

    private async Task<bool> SettleFeeChargeAsync(FeeCharge charge, DateTime now, CancellationToken cancellationToken)
    {
        if (charge.Status == FeeChargeStatus.Paid)
        {
            return true;
        }

        charge.MarkPaid(now);

        // The balance may have dropped since the Pix was issued (an admin settled part of it): never settle more than is owed.
        var balance = await _ledger.GetBalanceAsync(charge.DriverProfileId, cancellationToken);
        var amount = Math.Min(charge.Amount, balance);
        if (amount > 0)
        {
            await _ledger.AddAsync(
                FeeLedger.Settle(charge.DriverProfileId, null, amount, "Paid by Pix issued by the app"), cancellationToken);
            var driver = await _drivers.GetByIdAsync(charge.DriverProfileId, cancellationToken);
            if (driver is not null)
            {
                await _notifications.PublishAsync(
                    NotificationMessages.FeeSettled(driver.UserId, driver.Id, amount), cancellationToken);
            }
        }

        return true;
    }

    private async Task HandleSubscriptionEndedAsync(string gatewaySubscriptionId, CancellationToken cancellationToken)
    {
        var subscription = await _subscriptions.GetByGatewayIdAsync(gatewaySubscriptionId, cancellationToken);
        if (subscription is not null && subscription.Status != SubscriptionStatus.Cancelled)
        {
            subscription.CancelAtPeriodEnd(_clock.UtcNow);
        }
    }
}
