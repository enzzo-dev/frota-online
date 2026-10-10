using FrotaOnline.Application.Addresses.Dtos;
using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Addresses;

public interface IAddressService
{
    Task<Result<IReadOnlyList<AddressResponse>>> ListAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<AddressResponse>> CreateAsync(Guid userId, CreateAddressRequest request, CancellationToken cancellationToken);
}
