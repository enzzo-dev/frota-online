using FluentValidation;
using FrotaOnline.Application.Abstractions.Addresses;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Addresses.Dtos;
using FrotaOnline.Application.Common;
using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Addresses;

public sealed class AddressService : IAddressService
{
    private readonly IAddressRepository _addresses;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateAddressRequest> _validator;

    public AddressService(IAddressRepository addresses, IUnitOfWork unitOfWork, IValidator<CreateAddressRequest> validator)
    {
        _addresses = addresses;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<Result<IReadOnlyList<AddressResponse>>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var addresses = await _addresses.ListByOwnerAsync(userId, cancellationToken);
        return Result.Success<IReadOnlyList<AddressResponse>>(addresses.Select(ToResponse).ToList());
    }

    public async Task<Result<AddressResponse>> CreateAsync(
        Guid userId, CreateAddressRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<AddressResponse>(ValidationMapper.ToError(validation));
        }

        var address = Address.Create(
            userId, request.Label, request.Street, request.Number, request.Complement,
            request.District, request.City, request.State, BrazilianDocuments.OnlyDigits(request.ZipCode),
            request.Latitude, request.Longitude);

        await _addresses.AddAsync(address, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ToResponse(address));
    }

    public static AddressResponse ToResponse(Address a) => new(
        a.Id, a.Label, a.Street, a.Number, a.Complement, a.District, a.City, a.State, a.ZipCode, a.Latitude, a.Longitude);
}
