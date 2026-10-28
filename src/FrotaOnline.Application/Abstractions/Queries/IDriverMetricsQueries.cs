using FrotaOnline.Application.Marketplace.Dtos;

namespace FrotaOnline.Application.Abstractions.Queries;

public interface IDriverMetricsQueries
{
    Task<DriverMetricsResponse> GetAsync(Guid driverProfileId, DateTime? from, DateTime? to, CancellationToken cancellationToken);
}
