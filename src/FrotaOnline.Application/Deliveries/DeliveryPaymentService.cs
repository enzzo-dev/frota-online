using FluentValidation;
using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Deliveries.Dtos;
using FrotaOnline.Application.Drivers;
using FrotaOnline.Application.Marketplace;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Deliveries;

public sealed class DeliveryPaymentService : IDeliveryPaymentService
{
    private readonly IDriverRepository _drivers;
    private readonly IDeliveryRepository _deliveries;
    private readonly IPaymentRepository _payments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<ConfirmPaymentRequest> _validator;

    public DeliveryPaymentService(
        IDriverRepository drivers,
        IDeliveryRepository deliveries,
        IPaymentRepository payments,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IValidator<ConfirmPaymentRequest> validator)
    {
        _drivers = drivers;
        _deliveries = deliveries;
        _payments = payments;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _validator = validator;
    }

    public async Task<Result<PaymentResponse>> ConfirmAsync(
        Guid userId, Guid deliveryId, ConfirmPaymentRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<PaymentResponse>(ValidationMapper.ToError(validation));
        }

        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<PaymentResponse>(DriverErrors.ProfileNotFound);
        }

        var delivery = await _deliveries.GetByIdAsync(deliveryId, cancellationToken);
        if (delivery is null || delivery.DriverProfileId != profile.Id)
        {
            return Result.Failure<PaymentResponse>(MarketplaceErrors.DeliveryNotFound);
        }

        if (delivery.Status != DeliveryStatus.Delivered)
        {
            return Result.Failure<PaymentResponse>(DeliveryErrors.PaymentNotDelivered);
        }

        if (await _payments.ExistsForDeliveryAsync(delivery.Id, cancellationToken))
        {
            return Result.Failure<PaymentResponse>(DeliveryErrors.PaymentAlreadyConfirmed);
        }

        var payment = Payment.Create(delivery.Id, delivery.AgreedPrice, request.Method, paidOutsideApp: true);
        payment.MarkPaid(_clock.UtcNow, string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim());

        await _payments.AddAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new PaymentResponse(
            payment.Id, payment.DeliveryId, payment.Amount, payment.Method, payment.Status, payment.PaidAt));
    }
}
