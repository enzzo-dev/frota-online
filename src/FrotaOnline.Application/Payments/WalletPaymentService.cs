using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Payments;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Payments.Dtos;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Payments;

public interface IWalletPaymentService
{
    Task<Result<WalletPaymentResponse>> PayAsync(
        Guid customerId, Guid deliveryId, WalletPaymentRequest request, CancellationToken cancellationToken);

    /// <summary>Called when the provider reports a change: reads the payment's real status and settles or fails it.</summary>
    Task ReconcileAsync(string providerPaymentId, CancellationToken cancellationToken);
}

/// <summary>
/// The customer pays with a token the app made (Apple Pay through the Mercado Pago SDK). The payment is created with the
/// driver's own authorised account, so their share goes straight to them and the platform keeps only the fee. An approved
/// payment settles at once; one still under review settles when the provider's notification says so.
/// </summary>
public sealed class WalletPaymentService : IWalletPaymentService
{
    private readonly IFreightChargePreparer _preparer;
    private readonly IPaymentRepository _payments;
    private readonly IWalletAccountRepository _accounts;
    private readonly IWalletPaymentGateway _wallets;
    private readonly ISecretProtector _secrets;
    private readonly IPaymentWebhookService _webhooks;
    private readonly IDeliveryRepository _deliveries;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<WalletPaymentRequest> _validator;

    public WalletPaymentService(
        IFreightChargePreparer preparer,
        IPaymentRepository payments,
        IWalletAccountRepository accounts,
        IWalletPaymentGateway wallets,
        ISecretProtector secrets,
        IPaymentWebhookService webhooks,
        IDeliveryRepository deliveries,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IValidator<WalletPaymentRequest> validator)
    {
        _preparer = preparer;
        _payments = payments;
        _accounts = accounts;
        _wallets = wallets;
        _secrets = secrets;
        _webhooks = webhooks;
        _deliveries = deliveries;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _validator = validator;
    }

    public async Task<Result<WalletPaymentResponse>> PayAsync(
        Guid customerId, Guid deliveryId, WalletPaymentRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<WalletPaymentResponse>(ValidationMapper.ToError(validation));
        }

        if (!_wallets.IsConfigured)
        {
            return Result.Failure<WalletPaymentResponse>(PaymentErrors.GatewayNotConfigured);
        }

        var prepared = await _preparer.PrepareAsync(customerId, deliveryId, cancellationToken);
        if (prepared.IsFailure)
        {
            return Result.Failure<WalletPaymentResponse>(prepared.Error!);
        }

        var (delivery, driver, customer, driverShare, existing, _) = prepared.Value;
        var account = await _accounts.GetByDriverAsync(driver.Id, cancellationToken);
        if (account is null)
        {
            return Result.Failure<WalletPaymentResponse>(PaymentErrors.DriverCannotReceiveWallet);
        }

        WalletCharge charge;
        try
        {
            var accessToken = await FreshAccessTokenAsync(account, cancellationToken);
            charge = await _wallets.ChargeAsync(
                new WalletChargeRequest(
                    accessToken,
                    request.CardToken.Trim(),
                    request.PaymentMethodId.Trim(),
                    customer.Email,
                    delivery.AgreedPrice,
                    delivery.PlatformFee,
                    $"Frete {delivery.Id.ToString("N")[..8]}",
                    delivery.Id.ToString(),
                    IdempotencyKey(delivery.Id, request.CardToken)),
                cancellationToken);
        }
        catch (PaymentDeclinedException)
        {
            return Result.Failure<WalletPaymentResponse>(PaymentErrors.CardDeclined);
        }
        catch (PaymentGatewayException)
        {
            return Result.Failure<WalletPaymentResponse>(PaymentErrors.GatewayUnavailable);
        }

        if (existing is null)
        {
            existing = Payment.CreateOnline(delivery.Id, delivery.AgreedPrice, PaymentProviders.MercadoPago, charge.Id);
            await _payments.AddAsync(existing, cancellationToken);
        }
        else
        {
            existing.RestartOnline(PaymentProviders.MercadoPago, charge.Id);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _preparer.CancelReplacedPixAsync(prepared.Value, cancellationToken);

        if (charge.Status == "approved")
        {
            await _webhooks.HandleAsync(Notification(charge.Id, "approved", "PAYMENT_RECEIVED", delivery.AgreedPrice), cancellationToken);
        }

        return Result.Success(new WalletPaymentResponse(
            existing.Id, delivery.Id, existing.Amount, driverShare, delivery.PlatformFee, existing.Status, charge.Status));
    }

    public async Task ReconcileAsync(string providerPaymentId, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByGatewayIdAsync(providerPaymentId, cancellationToken);
        if (payment is null || payment.Status != PaymentStatus.Pending || payment.Gateway != PaymentProviders.MercadoPago)
        {
            return;
        }

        var delivery = await _deliveries.GetByIdAsync(payment.DeliveryId, cancellationToken);
        var account = delivery is null ? null : await _accounts.GetByDriverAsync(delivery.DriverProfileId, cancellationToken);
        if (account is null)
        {
            return;
        }

        // The notification only says "something changed": the status is read from the provider, never taken from the call.
        var accessToken = await FreshAccessTokenAsync(account, cancellationToken);
        var current = await _wallets.GetPaymentAsync(providerPaymentId, accessToken, cancellationToken);
        switch (current.Status)
        {
            case "approved":
                await _webhooks.HandleAsync(Notification(providerPaymentId, "approved", "PAYMENT_RECEIVED", payment.Amount), cancellationToken);
                break;
            case "rejected" or "cancelled":
                await _webhooks.HandleAsync(Notification(providerPaymentId, current.Status, "PAYMENT_FAILED", payment.Amount), cancellationToken);
                break;
        }
    }

    private async Task<string> FreshAccessTokenAsync(WalletAccount account, CancellationToken cancellationToken)
    {
        if (account.NeedsRefresh(_clock.UtcNow))
        {
            if (!_secrets.TryUnprotect(account.RefreshTokenProtected, out var refresh))
            {
                throw new PaymentGatewayException("Não foi possível ler o token de renovação salvo.");
            }

            var renewed = await _wallets.RefreshAsync(refresh, cancellationToken);
            account.UpdateTokens(
                renewed.ProviderUserId, _secrets.Protect(renewed.AccessToken), _secrets.Protect(renewed.RefreshToken), renewed.ExpiresAt);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return renewed.AccessToken;
        }

        if (!_secrets.TryUnprotect(account.AccessTokenProtected, out var access))
        {
            throw new PaymentGatewayException("Não foi possível ler o token de acesso salvo.");
        }

        return access;
    }

    private static WebhookNotification Notification(string paymentId, string status, string @event, decimal value) =>
        new($"mp:{paymentId}:{status}", @event, new WebhookPayment(paymentId, null, null, value), null);

    /// <summary>The same token for the same delivery always yields the same key, so a retried request cannot charge twice.</summary>
    private static string IdempotencyKey(Guid deliveryId, string cardToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{deliveryId:N}:{cardToken}")))[..40].ToLowerInvariant();
}
