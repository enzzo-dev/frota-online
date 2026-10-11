using System.Text.Json.Serialization;
using FrotaOnline.Application.Abstractions.Payments;
using FrotaOnline.Application.Payments;
using FrotaOnline.Application.Payments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/webhooks")]
[AllowAnonymous]
[ApiController]
public sealed class PaymentWebhooksController : ControllerBase
{
    private readonly IPaymentWebhookAuthenticator _authenticator;
    private readonly IPaymentWebhookService _webhooks;

    public PaymentWebhooksController(IPaymentWebhookAuthenticator authenticator, IPaymentWebhookService webhooks)
    {
        _authenticator = authenticator;
        _webhooks = webhooks;
    }

    /// <summary>
    /// Called by Asaas, not by the app. Authenticated by the shared token in the <c>asaas-access-token</c> header.
    /// Unknown events answer 200 so the provider does not retry them.
    /// </summary>
    [HttpPost("asaas")]
    [EndpointSummary("Asaas webhook")]
    [EndpointDescription("Receives payment and subscription events from Asaas. Not for the app: protected by the asaas-access-token header. Each event is processed once (the event id is the idempotency key); any event it does not use is acknowledged with 200.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Asaas([FromBody] AsaasEvent body, CancellationToken cancellationToken)
    {
        if (!_authenticator.IsValid(Request.Headers["asaas-access-token"].FirstOrDefault()))
        {
            return Unauthorized();
        }

        var payment = body.Payment is null
            ? null
            : new WebhookPayment(
                body.Payment.Id ?? string.Empty, body.Payment.Subscription, body.Payment.ExternalReference, body.Payment.Value);

        await _webhooks.HandleAsync(
            new WebhookNotification(body.Id ?? string.Empty, body.Event ?? string.Empty, payment, body.Subscription?.Id),
            cancellationToken);
        return Ok();
    }

    public sealed class AsaasEvent
    {
        [JsonPropertyName("id")] public string? Id { get; init; }

        [JsonPropertyName("event")] public string? Event { get; init; }

        [JsonPropertyName("payment")] public AsaasPayment? Payment { get; init; }

        [JsonPropertyName("subscription")] public AsaasSubscription? Subscription { get; init; }
    }

    public sealed class AsaasPayment
    {
        [JsonPropertyName("id")] public string? Id { get; init; }

        [JsonPropertyName("subscription")] public string? Subscription { get; init; }

        [JsonPropertyName("externalReference")] public string? ExternalReference { get; init; }

        [JsonPropertyName("value")] public decimal Value { get; init; }
    }

    public sealed class AsaasSubscription
    {
        [JsonPropertyName("id")] public string? Id { get; init; }
    }
}
