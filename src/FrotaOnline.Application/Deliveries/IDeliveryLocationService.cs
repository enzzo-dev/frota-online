using FrotaOnline.Application.Common;
using FrotaOnline.Application.Deliveries.Dtos;

namespace FrotaOnline.Application.Deliveries;

public interface IDeliveryLocationService
{
    /// <summary>Stores the driver's latest position. Only accepted while the delivery is under way.</summary>
    Task<Result> UpdateAsync(Guid userId, Guid deliveryId, UpdateLocationRequest request, CancellationToken cancellationToken);
}
