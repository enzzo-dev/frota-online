using FrotaOnline.Application.Common;
using FrotaOnline.Application.Deliveries.Dtos;

namespace FrotaOnline.Application.Deliveries;

public interface IFeeService
{
    Task<Result<FeeStatementResponse>> StatementForDriverAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<FeeStatementResponse>> StatementForAdminAsync(Guid driverProfileId, CancellationToken cancellationToken);

    /// <summary>Records money the driver paid to the platform. It cannot exceed the open balance.</summary>
    Task<Result<FeeStatementResponse>> SettleAsync(
        Guid driverProfileId, SettleFeeRequest request, CancellationToken cancellationToken);
}
