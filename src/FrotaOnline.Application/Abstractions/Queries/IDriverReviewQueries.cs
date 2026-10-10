using FrotaOnline.Application.Admin.Dtos;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Abstractions.Queries;

public interface IDriverReviewQueries
{
    /// <summary>Drivers in the given status, oldest first, joined with their user in the database.</summary>
    Task<IReadOnlyList<DriverReviewSummary>> ListByStatusAsync(DriverStatus status, CancellationToken cancellationToken);
}
