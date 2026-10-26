using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Abstractions.Marketplace;

public interface ISignatureRepository
{
    Task<bool> ExistsAsync(Guid freightRequestId, SignatureType type, CancellationToken cancellationToken);

    Task<Signature?> GetAsync(Guid freightRequestId, SignatureType type, CancellationToken cancellationToken);

    Task AddAsync(Signature signature, CancellationToken cancellationToken);
}
