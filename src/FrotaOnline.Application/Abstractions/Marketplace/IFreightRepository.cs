using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Abstractions.Marketplace;

public sealed record ExpiredFreight(Guid FreightId, Guid CustomerId);

public interface IFreightRepository
{
    Task<FreightRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Moves every open freight whose proposal window ended to Expired with one UPDATE statement and returns
    /// the freights that changed (so their customers can be told). Bypasses the change tracker on purpose.
    /// </summary>
    Task<IReadOnlyList<ExpiredFreight>> ExpireDueAsync(DateTime utcNow, CancellationToken cancellationToken);

    Task AddAsync(FreightRequest freight, CancellationToken cancellationToken);
}
