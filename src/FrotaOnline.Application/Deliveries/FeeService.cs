using FluentValidation;
using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Deliveries.Dtos;
using FrotaOnline.Application.Drivers;
using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Deliveries;

public sealed class FeeService : IFeeService
{
    private readonly IDriverRepository _drivers;
    private readonly IFeeLedgerRepository _ledger;
    private readonly IFeeQueries _queries;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<SettleFeeRequest> _validator;

    public FeeService(
        IDriverRepository drivers,
        IFeeLedgerRepository ledger,
        IFeeQueries queries,
        IUnitOfWork unitOfWork,
        IValidator<SettleFeeRequest> validator)
    {
        _drivers = drivers;
        _ledger = ledger;
        _queries = queries;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<Result<FeeStatementResponse>> StatementForDriverAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<FeeStatementResponse>(DriverErrors.ProfileNotFound);
        }

        return Result.Success(await _queries.GetStatementAsync(profile.Id, cancellationToken));
    }

    public async Task<Result<FeeStatementResponse>> StatementForAdminAsync(
        Guid driverProfileId, CancellationToken cancellationToken)
    {
        var driver = await _drivers.GetByIdAsync(driverProfileId, cancellationToken);
        if (driver is null)
        {
            return Result.Failure<FeeStatementResponse>(DriverErrors.ProfileNotFound);
        }

        return Result.Success(await _queries.GetStatementAsync(driver.Id, cancellationToken));
    }

    public async Task<Result<FeeStatementResponse>> SettleAsync(
        Guid driverProfileId, SettleFeeRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<FeeStatementResponse>(ValidationMapper.ToError(validation));
        }

        var driver = await _drivers.GetByIdAsync(driverProfileId, cancellationToken);
        if (driver is null)
        {
            return Result.Failure<FeeStatementResponse>(DriverErrors.ProfileNotFound);
        }

        var balance = await _ledger.GetBalanceAsync(driver.Id, cancellationToken);
        if (request.Amount > balance)
        {
            return Result.Failure<FeeStatementResponse>(DeliveryErrors.FeeExceedsBalance(balance));
        }

        await _ledger.AddAsync(
            FeeLedger.Settle(driver.Id, null, request.Amount, request.Description), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await _queries.GetStatementAsync(driver.Id, cancellationToken));
    }
}
