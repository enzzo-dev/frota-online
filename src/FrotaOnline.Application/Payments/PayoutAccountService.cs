using FluentValidation;
using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Drivers;
using FrotaOnline.Application.Payments.Dtos;

namespace FrotaOnline.Application.Payments;

public interface IPayoutAccountService
{
    Task<Result<PayoutAccountResponse>> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<PayoutAccountResponse>> UpdateAsync(Guid userId, UpdatePayoutAccountRequest request, CancellationToken cancellationToken);
}

/// <summary>The driver's own account at the payment provider, where the split of a Pix sends their share.</summary>
public sealed class PayoutAccountService : IPayoutAccountService
{
    private readonly IDriverRepository _drivers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<UpdatePayoutAccountRequest> _validator;

    public PayoutAccountService(
        IDriverRepository drivers, IUnitOfWork unitOfWork, IValidator<UpdatePayoutAccountRequest> validator)
    {
        _drivers = drivers;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<Result<PayoutAccountResponse>> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        return profile is null
            ? Result.Failure<PayoutAccountResponse>(DriverErrors.ProfileNotFound)
            : Result.Success(ToResponse(profile.PayoutWalletId));
    }

    public async Task<Result<PayoutAccountResponse>> UpdateAsync(
        Guid userId, UpdatePayoutAccountRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<PayoutAccountResponse>(ValidationMapper.ToError(validation));
        }

        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<PayoutAccountResponse>(DriverErrors.ProfileNotFound);
        }

        profile.SetPayoutWallet(request.WalletId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(profile.PayoutWalletId));
    }

    private static PayoutAccountResponse ToResponse(string? walletId) => new(walletId is not null, walletId);
}
