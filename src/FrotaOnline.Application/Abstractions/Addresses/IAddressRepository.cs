using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Abstractions.Addresses;

public interface IAddressRepository
{
    Task<Address?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Address>> ListByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Address>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task AddAsync(Address address, CancellationToken cancellationToken);
}
