using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Abstractions.Marketplace;

public interface IFeeLedgerRepository
{
    /// <summary>Accrued minus settled, summed by the database.</summary>
    Task<decimal> GetBalanceAsync(Guid driverProfileId, CancellationToken cancellationToken);

    Task AddAsync(FeeLedger entry, CancellationToken cancellationToken);
}
