using FluentValidation;
using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Payments;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Abstractions.Users;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Deliveries;
using FrotaOnline.Application.Payments.Dtos;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Payments;

public interface IPixPaymentService
{
    Task<Result<PixPaymentResponse>> CreateForDeliveryAsync(
        Guid customerId, Guid deliveryId, CreatePixRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// The customer pays the freight with a Pix the app issues. The provider splits it when it is received: the driver
/// gets the price minus the platform fee, straight to their own account; the platform keeps the fee. The platform
/// never holds the driver's money.
/// </summary>
public sealed class PixPaymentService : IPixPaymentService
{
    private readonly IDeliveryRepository _deliveries;
    private readonly IPaymentRepository _payments;
    private readonly IDriverRepository _drivers;
    private readonly IUserRepository _users;
    private readonly IPaymentGateway _gateway;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<CreatePixRequest> _validator;

    public PixPaymentService(
        IDeliveryRepository deliveries,
        IPaymentRepository payments,
        IDriverRepository drivers,
        IUserRepository users,
        IPaymentGateway gateway,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IValidator<CreatePixRequest> validator)
    {
        _deliveries = deliveries;
        _payments = payments;
        _drivers = drivers;
        _users = users;
        _gateway = gateway;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _validator = validator;
    }

    public async Task<Result<PixPaymentResponse>> CreateForDeliveryAsync(
        Guid customerId, Guid deliveryId, CreatePixRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<PixPaymentResponse>(ValidationMapper.ToError(validation));
        }

        var delivery = await _deliveries.GetByIdAsync(deliveryId, cancellationToken);
        if (delivery is null || delivery.CustomerId != customerId)
        {
            return Result.Failure<PixPaymentResponse>(PaymentErrors.NotYourDelivery);
        }

        if (delivery.Status != DeliveryStatus.Delivered)
        {
            return Result.Failure<PixPaymentResponse>(DeliveryErrors.PaymentNotDelivered);
        }

        if (!_gateway.IsConfigured)
        {
            return Result.Failure<PixPaymentResponse>(PaymentErrors.GatewayNotConfigured);
        }

        var now = _clock.UtcNow;
        var existing = await _payments.GetByDeliveryIdAsync(delivery.Id, cancellationToken);
        if (existing is not null)
        {
            // Asking again while the Pix is still good just returns it; a paid or outside-the-app payment is final.
            if (existing.IsPixUsable(now))
            {
                return Result.Success(ToResponse(existing, delivery));
            }

            if (existing.Status != PaymentStatus.Pending || existing.GatewayPaymentId is null)
            {
                return Result.Failure<PixPaymentResponse>(DeliveryErrors.PaymentAlreadyConfirmed);
            }
        }

        var driver = await _drivers.GetByIdAsync(delivery.DriverProfileId, cancellationToken);
        if (driver?.PayoutWalletId is null)
        {
            return Result.Failure<PixPaymentResponse>(PaymentErrors.DriverCannotReceive);
        }

        var driverShare = delivery.AgreedPrice - delivery.PlatformFee;
        if (driverShare <= 0 || delivery.AgreedPrice < PaymentErrors.MinimumCharge)
        {
            return Result.Failure<PixPaymentResponse>(PaymentErrors.PriceTooLow);
        }

        var customer = await _users.GetByIdAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return Result.Failure<PixPaymentResponse>(PaymentErrors.NotYourDelivery);
        }

        try
        {
            if (existing?.GatewayPaymentId is not null)
            {
                await TryCancelAsync(existing.GatewayPaymentId, cancellationToken);
            }

            var customerRef = await _gateway.CreateCustomerAsync(
                new GatewayCustomer(
                    customer.FullName, customer.Email, BrazilianDocuments.NormalizeTaxId(request.TaxId),
                    customer.PhoneNumber, customerId.ToString()),
                cancellationToken);

            var charge = await _gateway.CreatePixChargeAsync(
                new PixChargeRequest(
                    customerRef,
                    delivery.AgreedPrice,
                    DateOnly.FromDateTime(now),
                    $"Frete {delivery.Id.ToString("N")[..8]}",
                    delivery.Id.ToString(),
                    new[] { new GatewaySplit(driver.PayoutWalletId, driverShare) }),
                cancellationToken);

            if (existing is null)
            {
                existing = Payment.CreatePix(
                    delivery.Id, delivery.AgreedPrice, charge.Id, charge.Payload, charge.QrCodeImage, charge.ExpiresAt);
                await _payments.AddAsync(existing, cancellationToken);
            }
            else
            {
                existing.ReissuePix(charge.Id, charge.Payload, charge.QrCodeImage, charge.ExpiresAt);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(ToResponse(existing, delivery));
        }
        catch (PaymentGatewayException)
        {
            return Result.Failure<PixPaymentResponse>(PaymentErrors.GatewayUnavailable);
        }
    }

    private async Task TryCancelAsync(string chargeId, CancellationToken cancellationToken)
    {
        try
        {
            await _gateway.CancelChargeAsync(chargeId, cancellationToken);
        }
        catch (PaymentGatewayException)
        {
            // Already expired or removed at the provider: the new charge replaces it either way.
        }
    }

    private static PixPaymentResponse ToResponse(Payment payment, Delivery delivery) => new(
        payment.Id, payment.DeliveryId, payment.Amount, payment.Amount - delivery.PlatformFee, delivery.PlatformFee,
        payment.Status, payment.PixPayload!, payment.PixQrCode, payment.PixExpiresAt!.Value);
}
