using FrotaOnline.Application.Common;
using FrotaOnline.Application.Deliveries.Dtos;

namespace FrotaOnline.Application.Deliveries;

/// <summary>The money goes straight from customer to driver. The platform only records that it happened.</summary>
public interface IDeliveryPaymentService
{
    Task<Result<PaymentResponse>> ConfirmAsync(
        Guid userId, Guid deliveryId, ConfirmPaymentRequest request, CancellationToken cancellationToken);
}
