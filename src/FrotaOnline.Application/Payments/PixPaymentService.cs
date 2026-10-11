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
    private readonly IFreightChargePreparer _preparer;
    private readonly IPaymentRepository _payments;
    private readonly IPaymentGateway _gateway;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<CreatePixRequest> _validator;

    public PixPaymentService(
        IFreightChargePreparer preparer,
        IPaymentRepository payments,
        IPaymentGateway gateway,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IValidator<CreatePixRequest> validator)
    {
        _preparer = preparer;
        _payments = payments;
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

        var prepared = await _preparer.PrepareAsync(customerId, deliveryId, cancellationToken);
        if (prepared.IsFailure)
        {
            return Result.Failure<PixPaymentResponse>(prepared.Error!);
        }

        var (delivery, driver, customer, driverShare, existing, _) = prepared.Value;
        var now = _clock.UtcNow;

        // Asking again while the Pix is still good just returns it.
        if (existing is not null && existing.IsPixUsable(now))
        {
            return Result.Success(ToResponse(existing, delivery));
        }

        if (!_gateway.IsConfigured)
        {
            return Result.Failure<PixPaymentResponse>(PaymentErrors.GatewayNotConfigured);
        }

        if (driver.PayoutWalletId is null)
        {
            return Result.Failure<PixPaymentResponse>(PaymentErrors.DriverCannotReceive);
        }

        try
        {
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
            await _preparer.CancelReplacedPixAsync(prepared.Value, cancellationToken);
            return Result.Success(ToResponse(existing, delivery));
        }
        catch (PaymentGatewayException)
        {
            return Result.Failure<PixPaymentResponse>(PaymentErrors.GatewayUnavailable);
        }
    }

    private static PixPaymentResponse ToResponse(Payment payment, Delivery delivery) => new(
        payment.Id, payment.DeliveryId, payment.Amount, payment.Amount - delivery.PlatformFee, delivery.PlatformFee,
        payment.Status, payment.PixPayload!, payment.PixQrCode, payment.PixExpiresAt!.Value);
}
