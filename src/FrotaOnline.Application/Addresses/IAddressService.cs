using FrotaOnline.Application.Addresses.Dtos;
using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Addresses;

public interface IAddressService
{
    Task<Result<IReadOnlyList<AddressResponse>>> ListAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<AddressResponse>> CreateAsync(Guid userId, CreateAddressRequest request, CancellationToken cancellationToken);

    /// <summary>Replaces the address. Only while no freight ever used it.</summary>
    Task<Result<AddressResponse>> UpdateAsync(
        Guid userId, Guid addressId, CreateAddressRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Never used: removed for good. Used only by finished freights: hidden from the list, kept for their history.
    /// Used by an open freight or a delivery in progress: refused.
    /// </summary>
    Task<Result> DeleteAsync(Guid userId, Guid addressId, CancellationToken cancellationToken);
}
