using FrotaOnline.Application.Common;
using FrotaOnline.Application.Marketplace.Dtos;

namespace FrotaOnline.Application.Marketplace;

public interface ICargoPhotoService
{
    Task<Result> UploadAsync(Guid customerId, Guid freightId, UploadCargoPhotoRequest request, CancellationToken cancellationToken);

    Task<Result<CargoPhotoImage>> GetAsync(Guid userId, Guid freightId, CancellationToken cancellationToken);
}
