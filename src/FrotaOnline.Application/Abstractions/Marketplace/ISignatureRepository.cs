using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Abstractions.Marketplace;

public interface ISignatureRepository
{
    Task<bool> ExistsAsync(Guid freightRequestId, SignatureType type, CancellationToken cancellationToken);

    Task<bool> StoragePathExistsAsync(string storagePath, CancellationToken cancellationToken);

    Task AddAsync(Signature signature, CancellationToken cancellationToken);
}
