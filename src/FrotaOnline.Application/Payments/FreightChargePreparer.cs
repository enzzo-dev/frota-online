using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Payments;
using FrotaOnline.Application.Abstractions.Users;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Deliveries;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Payments;

/// <summary>What every online payment of a freight needs, checked once: who pays, who is paid, how much, and the earlier attempt.</summary>
public sealed record PreparedCharge(Delivery Delivery, DriverProfile Driver, User Customer, decimal DriverShare, Payment? Existing, string? PixToCancel);

public interface IFreightChargePreparer
{
    /// <summary>The delivery is the customer's, it is completed, the price covers the fee, and the earlier attempt (if any) allows a new one.</summary>
    Task<Result<PreparedCharge>> PrepareAsync(Guid customerId, Guid deliveryId, CancellationToken cancellationToken);

    /// <summary>After the new charge exists: cancels the Pix it replaces (best effort).</summary>
    Task CancelReplacedPixAsync(PreparedCharge prepared, CancellationToken cancellationToken);
}

public sealed class FreightChargePreparer : IFreightChargePreparer
{
    private readonly IDeliveryRepository _deliveries;
    private readonly IPaymentRepository _payments;
    private readonly IDriverRepository _drivers;
    private readonly IUserRepository _users;
    private readonly IPaymentGateway _gateway;

    public FreightChargePreparer(
        IDeliveryRepository deliveries,
        IPaymentRepository payments,
        IDriverRepository drivers,
        IUserRepository users,
        IPaymentGateway gateway)
    {
        _deliveries = deliveries;
        _payments = payments;
        _drivers = drivers;
        _users = users;
        _gateway = gateway;
    }

    public async Task<Result<PreparedCharge>> PrepareAsync(Guid customerId, Guid deliveryId, CancellationToken cancellationToken)
    {
        var delivery = await _deliveries.GetByIdAsync(deliveryId, cancellationToken);
        if (delivery is null || delivery.CustomerId != customerId)
        {
            return Result.Failure<PreparedCharge>(PaymentErrors.NotYourDelivery);
        }

        if (delivery.Status != DeliveryStatus.Delivered)
        {
            return Result.Failure<PreparedCharge>(DeliveryErrors.PaymentNotDelivered);
        }

        var existing = await _payments.GetByDeliveryIdAsync(delivery.Id, cancellationToken);
        if (existing is not null)
        {
            // Paid, or settled outside the app: final. A card/wallet payment still being confirmed: wait for it.
            if (existing.Status == PaymentStatus.Paid || (existing.Status == PaymentStatus.Pending && existing.GatewayPaymentId is null))
            {
                return Result.Failure<PreparedCharge>(DeliveryErrors.PaymentAlreadyConfirmed);
            }

            if (existing.Status == PaymentStatus.Pending && existing.Method == PaymentMethod.Card)
            {
                return Result.Failure<PreparedCharge>(PaymentErrors.PaymentInProgress);
            }
        }

        var driver = await _drivers.GetByIdAsync(delivery.DriverProfileId, cancellationToken);
        var customer = await _users.GetByIdAsync(customerId, cancellationToken);
        if (driver is null || customer is null)
        {
            return Result.Failure<PreparedCharge>(PaymentErrors.NotYourDelivery);
        }

        var driverShare = delivery.AgreedPrice - delivery.PlatformFee;
        if (driverShare <= 0 || delivery.AgreedPrice < PaymentErrors.MinimumCharge)
        {
            return Result.Failure<PreparedCharge>(PaymentErrors.PriceTooLow);
        }

        // Captured now: starting the new charge overwrites the id on the same payment.
        var pixToCancel = existing is { Status: PaymentStatus.Pending, Method: PaymentMethod.Pix } ? existing.GatewayPaymentId : null;
        return Result.Success(new PreparedCharge(delivery, driver, customer, driverShare, existing, pixToCancel));
    }

    public async Task CancelReplacedPixAsync(PreparedCharge prepared, CancellationToken cancellationToken)
    {
        if (prepared.PixToCancel is { } id)
        {
            try
            {
                await _gateway.CancelChargeAsync(id, cancellationToken);
            }
            catch (PaymentGatewayException)
            {
                // Already expired or removed at the provider.
            }
        }
    }
}
