using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Abstractions.Marketplace;

public interface IFreightRepository
{
    Task<FreightRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Moves every open freight whose proposal window ended to Expired with one UPDATE statement.
    /// Returns the number of rows changed. Bypasses the change tracker on purpose.
    /// </summary>
    Task<int> ExpireDueAsync(DateTime utcNow, CancellationToken cancellationToken);

    Task AddAsync(FreightRequest freight, CancellationToken cancellationToken);
}
