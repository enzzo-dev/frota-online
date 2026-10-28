using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Drivers;
using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Marketplace;

public sealed class DriverMetricsService : IDriverMetricsService
{
    private static readonly Error InvalidPeriod =
        new("metrics.invalid_period", "A data inicial deve ser anterior ou igual à data final.", ErrorType.Validation);

    private readonly IDriverRepository _drivers;
    private readonly IFreightRepository _freights;
    private readonly IFreightViewRepository _views;
    private readonly IDriverMetricsQueries _queries;
    private readonly IDateTimeProvider _clock;

    public DriverMetricsService(
        IDriverRepository drivers,
        IFreightRepository freights,
        IFreightViewRepository views,
        IDriverMetricsQueries queries,
        IDateTimeProvider clock)
    {
        _drivers = drivers;
        _freights = freights;
        _views = views;
        _queries = queries;
        _clock = clock;
    }

    public async Task<Result> RecordFreightViewAsync(Guid userId, Guid freightId, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure(DriverErrors.ProfileNotFound);
        }

        if (!profile.CanOperate)
        {
            return Result.Failure(MarketplaceErrors.DriverNotApproved);
        }

        var freight = await _freights.GetByIdAsync(freightId, cancellationToken);
        if (freight is null)
        {
            return Result.Failure(MarketplaceErrors.FreightNotFound);
        }

        if (freight.Status == FreightStatus.Open)
        {
            await _views.RecordAsync(freightId, profile.Id, _clock.UtcNow, cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result<DriverMetricsResponse>> GetAsync(
        Guid userId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<DriverMetricsResponse>(DriverErrors.ProfileNotFound);
        }

        var start = from?.ToUniversalTime();
        var end = to?.ToUniversalTime();
        if (start is not null && end is not null && start > end)
        {
            return Result.Failure<DriverMetricsResponse>(InvalidPeriod);
        }

        return Result.Success(await _queries.GetAsync(profile.Id, start, end, cancellationToken));
    }
}
