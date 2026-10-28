using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Abstractions.Marketplace;

public interface ICargoPhotoRepository
{
    Task<bool> ExistsAsync(Guid freightRequestId, CancellationToken cancellationToken);

    Task<FreightCargoPhoto?> GetAsync(Guid freightRequestId, CancellationToken cancellationToken);

    Task AddAsync(FreightCargoPhoto photo, CancellationToken cancellationToken);
}
