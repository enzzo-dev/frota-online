using FrotaOnline.Application.Common;
using FrotaOnline.Application.Deliveries.Dtos;

namespace FrotaOnline.Application.Deliveries;

public interface IDeliveryLocationService
{
    Task<Result> UpdateAsync(Guid userId, Guid deliveryId, UpdateLocationRequest request, CancellationToken cancellationToken);
}
