using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Payments;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Abstractions.Users;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Drivers;
using FrotaOnline.Application.Payments.Dtos;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Payments;

public interface IFeeChargeService
{
    Task<Result<FeeChargeResponse>> CreateAsync(Guid driverUserId, CancellationToken cancellationToken);
}

/// <summary>
/// Automatic collection of the platform fees for freights paid outside the app: the driver gets a Pix for the whole
/// open balance, and when it is paid the webhook settles the ledger by itself.
/// </summary>
public sealed class FeeChargeService : IFeeChargeService
{
    private readonly IDriverRepository _drivers;
    private readonly IUserRepository _users;
    private readonly IFeeLedgerRepository _ledger;
    private readonly IFeeChargeRepository _charges;
    private readonly IPaymentGateway _gateway;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public FeeChargeService(
        IDriverRepository drivers,
        IUserRepository users,
        IFeeLedgerRepository ledger,
        IFeeChargeRepository charges,
        IPaymentGateway gateway,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock)
    {
        _drivers = drivers;
        _users = users;
        _ledger = ledger;
        _charges = charges;
        _gateway = gateway;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<FeeChargeResponse>> CreateAsync(Guid driverUserId, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByUserIdAsync(driverUserId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<FeeChargeResponse>(DriverErrors.ProfileNotFound);
        }

        if (!_gateway.IsConfigured)
        {
            return Result.Failure<FeeChargeResponse>(PaymentErrors.GatewayNotConfigured);
        }

        var balance = await _ledger.GetBalanceAsync(profile.Id, cancellationToken);
        if (balance <= 0)
        {
            return Result.Failure<FeeChargeResponse>(PaymentErrors.NothingToCharge);
        }

        if (balance < PaymentErrors.MinimumCharge)
        {
            return Result.Failure<FeeChargeResponse>(PaymentErrors.FeeBelowMinimum(balance));
        }

        var now = _clock.UtcNow;
        var pending = await _charges.GetPendingByDriverAsync(profile.Id, cancellationToken);
        if (pending is not null && pending.IsUsable(now) && pending.Amount == balance)
        {
            return Result.Success(ToResponse(pending));
        }

        var user = await _users.GetByIdAsync(driverUserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<FeeChargeResponse>(DriverErrors.ProfileNotFound);
        }

        try
        {
            if (pending is not null)
            {
                // The balance changed or the Pix expired: the old one must not be payable any more.
                await TryCancelAsync(pending.GatewayPaymentId, cancellationToken);
                pending.Cancel();
            }

            if (profile.PaymentCustomerId is null)
            {
                var customerId = await _gateway.CreateCustomerAsync(
                    new GatewayCustomer(user.FullName, user.Email, profile.TaxId, user.PhoneNumber, profile.Id.ToString()),
                    cancellationToken);
                profile.LinkPaymentCustomer(customerId);
            }

            var charge = await _gateway.CreatePixChargeAsync(
                new PixChargeRequest(
                    profile.PaymentCustomerId!, balance, DateOnly.FromDateTime(now),
                    "Frota Online - taxa da plataforma", $"fee:{profile.Id}", Array.Empty<GatewaySplit>()),
                cancellationToken);

            var created = FeeCharge.Create(profile.Id, balance, charge.Id, charge.Payload, charge.QrCodeImage, charge.ExpiresAt);
            await _charges.AddAsync(created, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(ToResponse(created));
        }
        catch (PaymentGatewayException)
        {
            return Result.Failure<FeeChargeResponse>(PaymentErrors.GatewayUnavailable);
        }
    }

    private async Task TryCancelAsync(string chargeId, CancellationToken cancellationToken)
    {
        try
        {
            await _gateway.CancelChargeAsync(chargeId, cancellationToken);
        }
        catch (PaymentGatewayException)
        {
            // Already expired or removed at the provider.
        }
    }

    private static FeeChargeResponse ToResponse(FeeCharge c) => new(c.Id, c.Amount, c.Status, c.PixPayload, c.PixQrCode, c.ExpiresAt);
}
