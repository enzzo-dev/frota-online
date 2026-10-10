using FrotaOnline.Application.Deliveries.Dtos;

namespace FrotaOnline.Application.Abstractions.Queries;

public interface IFeeQueries
{
    /// <summary>Totals are SUMs in the database; entries are the 200 most recent.</summary>
    Task<FeeStatementResponse> GetStatementAsync(Guid driverProfileId, CancellationToken cancellationToken);
}
