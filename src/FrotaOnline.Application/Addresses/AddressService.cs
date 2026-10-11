using FluentValidation;
using FrotaOnline.Application.Abstractions.Addresses;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Addresses.Dtos;
using FrotaOnline.Application.Common;
using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Addresses;

public sealed class AddressService : IAddressService
{
    private readonly IAddressRepository _addresses;
    private readonly IAddressUsageQueries _usage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<CreateAddressRequest> _validator;

    public AddressService(
        IAddressRepository addresses,
        IAddressUsageQueries usage,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IValidator<CreateAddressRequest> validator)
    {
        _addresses = addresses;
        _usage = usage;
        _unitOfWork = unitOfWork;
        _clock = clock;
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

    public async Task<Result<AddressResponse>> UpdateAsync(
        Guid userId, Guid addressId, CreateAddressRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<AddressResponse>(ValidationMapper.ToError(validation));
        }

        var address = await _addresses.GetByIdAsync(addressId, cancellationToken);
        if (address is null || address.OwnerUserId != userId || address.IsDeleted)
        {
            return Result.Failure<AddressResponse>(AddressErrors.NotFound);
        }

        // Editing would rewrite where past and current freights go, so only an address nobody used can change.
        if (await _usage.GetUsageAsync(address.Id, cancellationToken) != AddressUsage.None)
        {
            return Result.Failure<AddressResponse>(AddressErrors.InUse);
        }

        address.Update(
            request.Label, request.Street, request.Number, request.Complement,
            request.District, request.City, request.State, BrazilianDocuments.OnlyDigits(request.ZipCode),
            request.Latitude, request.Longitude);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ToResponse(address));
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid addressId, CancellationToken cancellationToken)
    {
        var address = await _addresses.GetByIdAsync(addressId, cancellationToken);
        if (address is null || address.OwnerUserId != userId || address.IsDeleted)
        {
            return Result.Failure(AddressErrors.NotFound);
        }

        switch (await _usage.GetUsageAsync(address.Id, cancellationToken))
        {
            case AddressUsage.Active:
                return Result.Failure(AddressErrors.InActiveUse);
            case AddressUsage.History:
                address.MarkDeleted(_clock.UtcNow);
                break;
            default:
                _addresses.Remove(address);
                break;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public static AddressResponse ToResponse(Address a) => new(
        a.Id, a.Label, a.Street, a.Number, a.Complement, a.District, a.City, a.State, a.ZipCode, a.Latitude, a.Longitude);
}
