using FrotaOnline.Application.Deliveries;
using FrotaOnline.Application.Deliveries.Dtos;
using FrotaOnline.Application.Payments;
using FrotaOnline.Application.Payments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/deliveries")]
[Authorize(Roles = "Customer")]
[Produces("application/json")]
public sealed class CustomerDeliveriesController : ApiControllerBase
{
    private readonly ICustomerDeliveryService _deliveries;
    private readonly IDeliveryProgressService _progress;
    private readonly IPixPaymentService _pix;
    private readonly ICardPaymentService _cards;
    private readonly IWalletPaymentService _wallets;

    public CustomerDeliveriesController(
        ICustomerDeliveryService deliveries,
        IDeliveryProgressService progress,
        IPixPaymentService pix,
        ICardPaymentService cards,
        IWalletPaymentService wallets)
    {
        _deliveries = deliveries;
        _progress = progress;
        _pix = pix;
        _cards = cards;
        _wallets = wallets;
    }

    [HttpGet]
    [EndpointSummary("Deliveries of the customer")]
    [EndpointDescription("Newest first, with driver, vehicle, progress and payment status.")]
    [ProducesResponseType<IReadOnlyList<CustomerDeliverySummary>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Handle(await _deliveries.ListAsync(CurrentUserId, cancellationToken), Ok);

    [HttpGet("{deliveryId:guid}")]
    [EndpointSummary("Follow a delivery")]
    [EndpointDescription("Status, stops with completion times and the event timeline.")]
    [ProducesResponseType<CustomerDeliveryDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid deliveryId, CancellationToken cancellationToken) =>
        Handle(await _deliveries.GetAsync(CurrentUserId, deliveryId, cancellationToken), Ok);

    [HttpPost("{deliveryId:guid}/cancel")]
    [EndpointSummary("Cancel a delivery")]
    [EndpointDescription("Only before the cargo is picked up (Scheduled or ToPickup).")]
    [ProducesResponseType<DeliveryProgressResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        Guid deliveryId, [FromBody] CancelDeliveryRequest request, CancellationToken cancellationToken) =>
        Handle(await _progress.CancelAsCustomerAsync(CurrentUserId, deliveryId, request, cancellationToken), Ok);

    [HttpPost("{deliveryId:guid}/pix")]
    [EndpointSummary("Pay the freight by Pix, issued by the app")]
    [EndpointDescription("After the delivery is completed. Body: { \"taxId\": \"<CPF or CNPJ of the payer>\" } (the payment provider requires it). Returns the Pix: pixPayload (copia e cola), pixQrCode (base64 PNG), expiresAt, and how it is split: driverReceives goes straight to the driver's own account, platformFee stays with the platform. The platform never holds the driver's money. When the Pix is paid the payment becomes Paid by itself and both sides are notified. Asking again returns the same Pix while it is valid. 409 payments.driver_without_payout_account when the driver has not linked an account yet (pay the driver directly and let them confirm instead).")]
    [ProducesResponseType<PixPaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Pix(Guid deliveryId, [FromBody] CreatePixRequest request, CancellationToken cancellationToken) =>
        Handle(await _pix.CreateForDeliveryAsync(CurrentUserId, deliveryId, request, cancellationToken), Ok);

    [HttpPost("{deliveryId:guid}/card")]
    [EndpointSummary("Pay the freight by credit card")]
    [EndpointDescription("After the delivery is completed. Body: { \"savedCardId\": \"<uuid>\" } to use a saved card, or { \"card\": { \"holderName\", \"number\", \"expiryMonth\": \"MM\", \"expiryYear\": \"YYYY\", \"ccv\" }, \"holder\": { \"taxId\", \"postalCode\", \"addressNumber\", \"phone\" }, \"saveCard\": true|false } for a new one. The card goes to the payment provider in this one request; the app server never stores or logs it, only the provider's token when saveCard is true. The payment is split by the provider: driverReceives goes to the driver's account, platformFee stays with the platform. The status stays Pending until the provider confirms (a notification arrives); a refused card answers 409 payments.card_declined and nothing is charged. Use HTTPS only.")]
    [ProducesResponseType<CardPaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Card(Guid deliveryId, [FromBody] PayWithCardRequest request, CancellationToken cancellationToken) =>
        Handle(
            await _cards.PayAsync(
                CurrentUserId, deliveryId, request, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0", cancellationToken),
            Ok);

    [HttpPost("{deliveryId:guid}/wallet")]
    [EndpointSummary("Pay the freight with Apple Pay (Mercado Pago)")]
    [EndpointDescription("After the delivery is completed. The app opens Apple Pay with the Mercado Pago SDK, which returns a card token; send it here: { \"cardToken\": \"...\", \"paymentMethodId\": \"visa\" }. The payment is created on the driver's own connected account and the platform keeps only its fee. approved settles at once; in_process settles when the provider confirms. 409 payments.driver_without_wallet_account when the driver has not connected their Mercado Pago account (pay by Pix or card instead).")]
    [ProducesResponseType<WalletPaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Wallet(Guid deliveryId, [FromBody] WalletPaymentRequest request, CancellationToken cancellationToken) =>
        Handle(await _wallets.PayAsync(CurrentUserId, deliveryId, request, cancellationToken), Ok);
}
