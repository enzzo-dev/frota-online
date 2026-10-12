using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Payments;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Payments;

public interface ISubscriptionPolicy
{
    /// <summary>The basic plan covers a few vehicles; the full plan has no limit. Without a plan there is no limit here.</summary>
    Task<Result> EnsureCanAddVehicleAsync(Guid driverProfileId, int activeVehicles, CancellationToken cancellationToken);

    /// <summary>
    /// Only enforced when <c>Subscriptions:RequiredToPropose</c> is on. A driver without a plan may take one freight, once
    /// (<see cref="FreeFreightsWithoutPlan"/>); after that a paid period is needed.
    /// </summary>
    Task<Result> EnsureCanProposeAsync(Guid driverProfileId, CancellationToken cancellationToken);

    /// <summary>
    /// The same rule when the customer approves a proposal. A driver can have sent several proposals while the free freight
    /// was still unused; only the first approval gets it.
    /// </summary>
    Task<Result> EnsureCanBeApprovedAsync(Guid driverProfileId, CancellationToken cancellationToken);
}

public sealed class SubscriptionPolicy : ISubscriptionPolicy
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly ISubscriptionSettings _settings;
    private readonly IDeliveryRepository _deliveries;
    private readonly IDateTimeProvider _clock;

    /// <summary>Freights a driver may take without ever having a paid plan.</summary>
    public const int FreeFreightsWithoutPlan = 1;

    public SubscriptionPolicy(
        ISubscriptionRepository subscriptions, ISubscriptionSettings settings, IDeliveryRepository deliveries, IDateTimeProvider clock)
    {
        _subscriptions = subscriptions;
        _settings = settings;
        _deliveries = deliveries;
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

    public Task<Result> EnsureCanProposeAsync(Guid driverProfileId, CancellationToken cancellationToken) =>
        EnsureAccessOrFreeFreightAsync(driverProfileId, cancellationToken);

    public Task<Result> EnsureCanBeApprovedAsync(Guid driverProfileId, CancellationToken cancellationToken) =>
        EnsureAccessOrFreeFreightAsync(driverProfileId, cancellationToken);

    private async Task<Result> EnsureAccessOrFreeFreightAsync(Guid driverProfileId, CancellationToken cancellationToken)
    {
        if (!_settings.RequiredToPropose)
        {
            return Result.Success();
        }

        var now = _clock.UtcNow;
        var current = await _subscriptions.GetCurrentAsync(driverProfileId, now, cancellationToken);
        if (current is not null && current.HasAccess(now))
        {
            return Result.Success();
        }

        // No paid period: the driver may still take the free freight. A cancelled delivery gives it back.
        var taken = await _deliveries.CountTakenByDriverAsync(driverProfileId, cancellationToken);
        return taken < FreeFreightsWithoutPlan
            ? Result.Success()
            : Result.Failure(SubscriptionErrors.Required);
    }
}
