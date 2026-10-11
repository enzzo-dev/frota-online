using FluentValidation;
using FrotaOnline.Application.Abstractions.Payments;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Payments.Dtos;
using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Payments;

public interface ICardPaymentService
{
    Task<Result<CardPaymentResponse>> PayAsync(
        Guid customerId, Guid deliveryId, PayWithCardRequest request, string remoteIp, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<SavedCardResponse>>> ListCardsAsync(Guid customerId, CancellationToken cancellationToken);

    Task<Result> RemoveCardAsync(Guid customerId, Guid cardId, CancellationToken cancellationToken);
}

/// <summary>
/// The customer pays the freight with a credit card. The card goes to the provider in one request and is never stored or
/// logged; the provider splits the payment (driver's share to their account, the fee to the platform). The payment stays
/// pending until the provider's webhook confirms it, which is also what settles the platform fee. A card the customer chose
/// to keep is stored as the provider's token only.
/// </summary>
public sealed class CardPaymentService : ICardPaymentService
{
    private readonly IFreightChargePreparer _preparer;
    private readonly IPaymentRepository _payments;
    private readonly ISavedCardRepository _cards;
    private readonly IPaymentGateway _gateway;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<PayWithCardRequest> _validator;

    public CardPaymentService(
        IFreightChargePreparer preparer,
        IPaymentRepository payments,
        ISavedCardRepository cards,
        IPaymentGateway gateway,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IValidator<PayWithCardRequest> validator)
    {
        _preparer = preparer;
        _payments = payments;
        _cards = cards;
        _gateway = gateway;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _validator = validator;
    }

    public async Task<Result<CardPaymentResponse>> PayAsync(
        Guid customerId, Guid deliveryId, PayWithCardRequest request, string remoteIp, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<CardPaymentResponse>(ValidationMapper.ToError(validation));
        }

        var prepared = await _preparer.PrepareAsync(customerId, deliveryId, cancellationToken);
        if (prepared.IsFailure)
        {
            return Result.Failure<CardPaymentResponse>(prepared.Error!);
        }

        if (!_gateway.IsConfigured)
        {
            return Result.Failure<CardPaymentResponse>(PaymentErrors.GatewayNotConfigured);
        }

        var (delivery, driver, customer, driverShare, existing, _) = prepared.Value;
        if (driver.PayoutWalletId is null)
        {
            return Result.Failure<CardPaymentResponse>(PaymentErrors.DriverCannotReceive);
        }

        SavedCard? saved = null;
        if (request.SavedCardId is { } savedId)
        {
            saved = await _cards.GetAsync(customerId, savedId, cancellationToken);
            if (saved is null)
            {
                return Result.Failure<CardPaymentResponse>(PaymentErrors.CardNotFound);
            }
        }

        CardCharge charge;
        string customerRef;
        try
        {
            customerRef = saved?.GatewayCustomerId ?? await _gateway.CreateCustomerAsync(
                new GatewayCustomer(
                    customer.FullName, customer.Email, BrazilianDocuments.NormalizeTaxId(request.Holder!.TaxId),
                    customer.PhoneNumber ?? request.Holder.Phone, customerId.ToString()),
                cancellationToken);

            charge = await _gateway.CreateCardChargeAsync(
                new CardChargeRequest(
                    customerRef,
                    delivery.AgreedPrice,
                    DateOnly.FromDateTime(_clock.UtcNow),
                    $"Frete {delivery.Id.ToString("N")[..8]}",
                    delivery.Id.ToString(),
                    new[] { new GatewaySplit(driver.PayoutWalletId, driverShare) },
                    remoteIp,
                    saved is null ? ToDetails(request.Card!) : null,
                    saved is null ? ToHolder(customer, request.Holder!) : null,
                    saved?.Token),
                cancellationToken);
        }
        catch (PaymentDeclinedException)
        {
            return Result.Failure<CardPaymentResponse>(PaymentErrors.CardDeclined);
        }
        catch (PaymentGatewayException)
        {
            return Result.Failure<CardPaymentResponse>(PaymentErrors.GatewayUnavailable);
        }

        if (existing is null)
        {
            existing = Payment.CreateOnline(delivery.Id, delivery.AgreedPrice, PaymentProviders.Asaas, charge.Id);
            await _payments.AddAsync(existing, cancellationToken);
        }
        else
        {
            existing.RestartOnline(PaymentProviders.Asaas, charge.Id);
        }

        var cardSaved = false;
        if (saved is null && request.SaveCard && charge.Token is not null)
        {
            var count = (await _cards.ListByUserAsync(customerId, cancellationToken)).Count;
            if (count < SavedCard.MaxPerUser)
            {
                await _cards.AddAsync(
                    SavedCard.Create(customerId, customerRef, charge.Token, charge.Brand ?? string.Empty, charge.Last4 ?? string.Empty),
                    cancellationToken);
                cardSaved = true;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _preparer.CancelReplacedPixAsync(prepared.Value, cancellationToken);

        return Result.Success(new CardPaymentResponse(
            existing.Id, delivery.Id, existing.Amount, driverShare, delivery.PlatformFee, existing.Status,
            saved?.Brand ?? charge.Brand, saved?.Last4 ?? charge.Last4, cardSaved));
    }

    public async Task<Result<IReadOnlyList<SavedCardResponse>>> ListCardsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var cards = await _cards.ListByUserAsync(customerId, cancellationToken);
        return Result.Success<IReadOnlyList<SavedCardResponse>>(
            cards.Select(c => new SavedCardResponse(c.Id, c.Brand, c.Last4)).ToList());
    }

    public async Task<Result> RemoveCardAsync(Guid customerId, Guid cardId, CancellationToken cancellationToken)
    {
        var card = await _cards.GetAsync(customerId, cardId, cancellationToken);
        if (card is null)
        {
            return Result.Failure(PaymentErrors.CardNotFound);
        }

        _cards.Remove(card);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static CardDetails ToDetails(CardInput card) => new(
        card.HolderName.Trim(),
        new string(card.Number.Where(char.IsAsciiDigit).ToArray()),
        card.ExpiryMonth.PadLeft(2, '0'),
        card.ExpiryYear,
        card.Ccv);

    private static CardHolderDetails ToHolder(User customer, CardHolderInput holder) => new(
        customer.FullName,
        customer.Email,
        BrazilianDocuments.NormalizeTaxId(holder.TaxId),
        new string(holder.PostalCode.Where(char.IsAsciiDigit).ToArray()),
        holder.AddressNumber.Trim(),
        customer.PhoneNumber ?? holder.Phone);
}
