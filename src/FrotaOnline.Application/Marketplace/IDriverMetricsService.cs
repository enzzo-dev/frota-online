using FrotaOnline.Application.Common;
using FrotaOnline.Application.Marketplace.Dtos;

namespace FrotaOnline.Application.Marketplace;

public interface IDriverMetricsService
{
    Task<Result> RecordFreightViewAsync(Guid userId, Guid freightId, CancellationToken cancellationToken);

    Task<Result<DriverMetricsResponse>> GetAsync(Guid userId, DateTime? from, DateTime? to, CancellationToken cancellationToken);
}
