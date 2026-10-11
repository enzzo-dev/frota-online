using FluentValidation;
using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Payments;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Abstractions.Users;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Drivers;
using FrotaOnline.Application.Marketplace;
using FrotaOnline.Application.Payments.Dtos;
using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Payments;

public interface ISubscriptionService
{
    Task<Result<SubscriptionOverview>> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<SubscriptionResponse>> SubscribeAsync(Guid userId, SubscribeRequest request, CancellationToken cancellationToken);

    Task<Result<SubscriptionResponse>> CancelAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed class SubscriptionService : ISubscriptionService
{
    private readonly IDriverRepository _drivers;
    private readonly IUserRepository _users;
    private readonly IVehicleRepository _vehicles;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IPaymentGateway _gateway;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<SubscribeRequest> _validator;

    public SubscriptionService(
        IDriverRepository drivers,
        IUserRepository users,
        IVehicleRepository vehicles,
        ISubscriptionRepository subscriptions,
        IPaymentGateway gateway,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IValidator<SubscribeRequest> validator)
    {
        _drivers = drivers;
        _users = users;
        _vehicles = vehicles;
        _subscriptions = subscriptions;
        _gateway = gateway;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _validator = validator;
    }

    private static readonly IReadOnlyList<PlanOption> Plans = new[]
    {
        new PlanOption(SubscriptionPlan.Basic, SubscriptionPricing.Basic, SubscriptionPricing.BasicMaxVehicles),
        new PlanOption(SubscriptionPlan.Full, SubscriptionPricing.Full, null)
    };

    public async Task<Result<SubscriptionOverview>> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<SubscriptionOverview>(DriverErrors.ProfileNotFound);
        }

        var now = _clock.UtcNow;
        var current = await _subscriptions.GetCurrentAsync(profile.Id, now, cancellationToken);
        return Result.Success(new SubscriptionOverview(Plans, current is null ? null : ToResponse(current, now)));
    }

    public async Task<Result<SubscriptionResponse>> SubscribeAsync(
        Guid userId, SubscribeRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<SubscriptionResponse>(ValidationMapper.ToError(validation));
        }

        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<SubscriptionResponse>(DriverErrors.ProfileNotFound);
        }

        if (!profile.CanOperate)
        {
            return Result.Failure<SubscriptionResponse>(MarketplaceErrors.DriverNotApproved);
        }

        if (!_gateway.IsConfigured)
        {
            return Result.Failure<SubscriptionResponse>(PaymentErrors.GatewayNotConfigured);
        }

        var now = _clock.UtcNow;
        var current = await _subscriptions.GetCurrentAsync(profile.Id, now, cancellationToken);
        if (current is not null && current.Status != SubscriptionStatus.Pending && current.HasAccess(now))
        {
            return Result.Failure<SubscriptionResponse>(SubscriptionErrors.AlreadyActive);
        }

        // Asked twice for the same plan before paying: hand back the same payment page.
        if (current is { Status: SubscriptionStatus.Pending } && current.Plan == request.Plan && current.PaymentUrl is not null)
        {
            return Result.Success(ToResponse(current, now));
        }

        if (request.Plan == SubscriptionPlan.Basic)
        {
            var vehicles = await _vehicles.ListActiveByDriverAsync(profile.Id, cancellationToken);
            if (vehicles.Count > SubscriptionPricing.BasicMaxVehicles)
            {
                return Result.Failure<SubscriptionResponse>(SubscriptionErrors.TooManyVehicles(SubscriptionPricing.BasicMaxVehicles));
            }
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<SubscriptionResponse>(DriverErrors.ProfileNotFound);
        }

        try
        {
            if (current is { Status: SubscriptionStatus.Pending })
            {
                // A different plan was chosen: the unpaid charge of the old one must not stay open.
                if (current.GatewaySubscriptionId is not null)
                {
                    await _gateway.CancelSubscriptionAsync(current.GatewaySubscriptionId, cancellationToken);
                }

                current.Cancel(now);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            if (profile.PaymentCustomerId is null)
            {
                var customerId = await _gateway.CreateCustomerAsync(
                    new GatewayCustomer(user.FullName, user.Email, profile.TaxId, user.PhoneNumber, profile.Id.ToString()),
                    cancellationToken);
                profile.LinkPaymentCustomer(customerId);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var subscription = Subscription.Create(profile.Id, request.Plan, user.PhoneNumber);
            var created = await _gateway.CreateSubscriptionAsync(
                profile.PaymentCustomerId!,
                subscription.MonthlyPrice,
                DateOnly.FromDateTime(now),
                $"Frota Online - plano {(request.Plan == SubscriptionPlan.Basic ? "Básico" : "Completo")}",
                subscription.Id.ToString(),
                cancellationToken);

            subscription.LinkGateway(created.Id, created.PaymentUrl);
            await _subscriptions.AddAsync(subscription, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(ToResponse(subscription, now));
        }
        catch (PaymentGatewayException)
        {
            return Result.Failure<SubscriptionResponse>(PaymentErrors.GatewayUnavailable);
        }
    }

    public async Task<Result<SubscriptionResponse>> CancelAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<SubscriptionResponse>(DriverErrors.ProfileNotFound);
        }

        var now = _clock.UtcNow;
        var current = await _subscriptions.GetCurrentAsync(profile.Id, now, cancellationToken);
        if (current is null || current.Status == SubscriptionStatus.Cancelled)
        {
            return Result.Failure<SubscriptionResponse>(SubscriptionErrors.NotFound);
        }

        try
        {
            if (current.GatewaySubscriptionId is not null && _gateway.IsConfigured)
            {
                await _gateway.CancelSubscriptionAsync(current.GatewaySubscriptionId, cancellationToken);
            }
        }
        catch (PaymentGatewayException)
        {
            return Result.Failure<SubscriptionResponse>(PaymentErrors.GatewayUnavailable);
        }

        if (current.Status == SubscriptionStatus.Pending)
        {
            current.Cancel(now);
        }
        else
        {
            current.CancelAtPeriodEnd(now);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(current, now));
    }

    private static SubscriptionResponse ToResponse(Subscription s, DateTime now) => new(
        s.Id, s.Plan, s.Status, s.MonthlyPrice,
        s.Plan == SubscriptionPlan.Basic ? s.MaxVehicles : null,
        s.HasAccess(now), s.StartsAt, s.EndsAt,
        s.Status == SubscriptionStatus.Pending ? s.PaymentUrl : null);
}
