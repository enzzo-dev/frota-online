using FrotaOnline.Application.Abstractions.Payments;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Payments;

public interface ISubscriptionPolicy
{
    /// <summary>The basic plan covers a few vehicles; the full plan has no limit. Without a plan there is no limit here.</summary>
    Task<Result> EnsureCanAddVehicleAsync(Guid driverProfileId, int activeVehicles, CancellationToken cancellationToken);

    /// <summary>Only enforced when <c>Subscriptions:RequiredToPropose</c> is on.</summary>
    Task<Result> EnsureCanProposeAsync(Guid driverProfileId, CancellationToken cancellationToken);
}

public sealed class SubscriptionPolicy : ISubscriptionPolicy
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly ISubscriptionSettings _settings;
    private readonly IDateTimeProvider _clock;

    public SubscriptionPolicy(ISubscriptionRepository subscriptions, ISubscriptionSettings settings, IDateTimeProvider clock)
    {
        _subscriptions = subscriptions;
        _settings = settings;
        _clock = clock;
    }

    public async Task<Result> EnsureCanAddVehicleAsync(
        Guid driverProfileId, int activeVehicles, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var current = await _subscriptions.GetCurrentAsync(driverProfileId, now, cancellationToken);
        if (current is { Plan: SubscriptionPlan.Basic } && current.HasAccess(now) && activeVehicles >= current.MaxVehicles)
        {
            return Result.Failure(SubscriptionErrors.VehicleLimitReached(current.MaxVehicles));
        }

        return Result.Success();
    }

    public async Task<Result> EnsureCanProposeAsync(Guid driverProfileId, CancellationToken cancellationToken)
    {
        if (!_settings.RequiredToPropose)
        {
            return Result.Success();
        }

        var now = _clock.UtcNow;
        var current = await _subscriptions.GetCurrentAsync(driverProfileId, now, cancellationToken);
        return current is not null && current.HasAccess(now)
            ? Result.Success()
            : Result.Failure(SubscriptionErrors.Required);
    }
}
