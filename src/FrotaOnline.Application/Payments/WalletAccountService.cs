using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Payments;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Drivers;
using FrotaOnline.Application.Payments.Dtos;
using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Payments;

public interface IWalletAccountService
{
    Task<Result<WalletAccountResponse>> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Link of the page where the driver authorises their Mercado Pago account.</summary>
    Task<Result<WalletAuthorizationResponse>> StartAuthorizationAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Called by the provider's redirect after the driver allows access. <paramref name="state"/> ties it to the driver.</summary>
    Task<Result> CompleteAuthorizationAsync(string code, string state, CancellationToken cancellationToken);

    Task<Result<WalletAccountResponse>> DisconnectAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>The driver connects their own Mercado Pago account once, so wallet payments pay them directly.</summary>
public sealed class WalletAccountService : IWalletAccountService
{
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(15);

    private readonly IDriverRepository _drivers;
    private readonly IWalletAccountRepository _accounts;
    private readonly IWalletPaymentGateway _wallets;
    private readonly ISecretProtector _secrets;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public WalletAccountService(
        IDriverRepository drivers,
        IWalletAccountRepository accounts,
        IWalletPaymentGateway wallets,
        ISecretProtector secrets,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock)
    {
        _drivers = drivers;
        _accounts = accounts;
        _wallets = wallets;
        _secrets = secrets;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<WalletAccountResponse>> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var driver = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (driver is null)
        {
            return Result.Failure<WalletAccountResponse>(DriverErrors.ProfileNotFound);
        }

        return Result.Success(new WalletAccountResponse(await _accounts.GetByDriverAsync(driver.Id, cancellationToken) is not null));
    }

    public async Task<Result<WalletAuthorizationResponse>> StartAuthorizationAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!_wallets.IsConfigured)
        {
            return Result.Failure<WalletAuthorizationResponse>(PaymentErrors.GatewayNotConfigured);
        }

        var driver = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (driver is null)
        {
            return Result.Failure<WalletAuthorizationResponse>(DriverErrors.ProfileNotFound);
        }

        var expires = _clock.UtcNow.Add(StateLifetime).Ticks;
        var state = _secrets.Protect($"{driver.Id:N}|{expires}");
        return Result.Success(new WalletAuthorizationResponse(_wallets.BuildAuthorizationUrl(state)));
    }

    public async Task<Result> CompleteAuthorizationAsync(string code, string state, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || !_secrets.TryUnprotect(state, out var plain))
        {
            return Result.Failure(PaymentErrors.WalletAuthorizationInvalid);
        }

        var parts = plain.Split('|');
        if (parts.Length != 2
            || !Guid.TryParseExact(parts[0], "N", out var driverId)
            || !long.TryParse(parts[1], out var ticks)
            || _clock.UtcNow.Ticks > ticks)
        {
            return Result.Failure(PaymentErrors.WalletAuthorizationInvalid);
        }

        var driver = await _drivers.GetByIdAsync(driverId, cancellationToken);
        if (driver is null)
        {
            return Result.Failure(PaymentErrors.WalletAuthorizationInvalid);
        }

        SellerAuthorization authorization;
        try
        {
            authorization = await _wallets.ExchangeCodeAsync(code, cancellationToken);
        }
        catch (PaymentGatewayException)
        {
            return Result.Failure(PaymentErrors.GatewayUnavailable);
        }

        var access = _secrets.Protect(authorization.AccessToken);
        var refresh = _secrets.Protect(authorization.RefreshToken);
        var account = await _accounts.GetByDriverAsync(driver.Id, cancellationToken);
        if (account is null)
        {
            await _accounts.AddAsync(
                WalletAccount.Create(driver.Id, authorization.ProviderUserId, access, refresh, authorization.ExpiresAt), cancellationToken);
        }
        else
        {
            account.UpdateTokens(authorization.ProviderUserId, access, refresh, authorization.ExpiresAt);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<WalletAccountResponse>> DisconnectAsync(Guid userId, CancellationToken cancellationToken)
    {
        var driver = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (driver is null)
        {
            return Result.Failure<WalletAccountResponse>(DriverErrors.ProfileNotFound);
        }

        var account = await _accounts.GetByDriverAsync(driver.Id, cancellationToken);
        if (account is not null)
        {
            _accounts.Remove(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new WalletAccountResponse(false));
    }
}
