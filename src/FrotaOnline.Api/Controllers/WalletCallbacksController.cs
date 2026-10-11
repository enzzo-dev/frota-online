using FrotaOnline.Application.Abstractions.Payments;
using FrotaOnline.Application.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace FrotaOnline.Api.Controllers;

/// <summary>What Mercado Pago calls: the driver's browser coming back from the authorisation page, and payment notifications.</summary>
[AllowAnonymous]
[ApiController]
public sealed class WalletCallbacksController : ControllerBase
{
    private readonly IWalletAccountService _accounts;
    private readonly IWalletPaymentService _payments;
    private readonly IWalletWebhookAuthenticator _authenticator;
    private readonly IConfiguration _configuration;

    public WalletCallbacksController(
        IWalletAccountService accounts,
        IWalletPaymentService payments,
        IWalletWebhookAuthenticator authenticator,
        IConfiguration configuration)
    {
        _accounts = accounts;
        _payments = payments;
        _authenticator = authenticator;
        _configuration = configuration;
    }

    [HttpGet("api/v1/callbacks/mercadopago/oauth")]
    [EndpointSummary("Mercado Pago authorisation callback")]
    [EndpointDescription("Where Mercado Pago sends the driver's browser after they allow access. Not for the app. The state ties the call to the driver and expires in 15 minutes. Redirects to the app (Payments:MercadoPago:AppReturnUrl) with status=ok or status=error when configured; otherwise answers with a short message.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> OAuth(
        [FromQuery] string? code, [FromQuery] string? state, CancellationToken cancellationToken)
    {
        var result = await _accounts.CompleteAuthorizationAsync(code ?? string.Empty, state ?? string.Empty, cancellationToken);
        var returnUrl = _configuration["Payments:MercadoPago:AppReturnUrl"];
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            var separator = returnUrl.Contains('?') ? '&' : '?';
            return Redirect($"{returnUrl}{separator}status={(result.IsSuccess ? "ok" : "error")}");
        }

        return result.IsSuccess
            ? Content("Conta do Mercado Pago conectada. Você já pode voltar ao aplicativo.")
            : BadRequest("Não foi possível conectar a conta. Volte ao aplicativo e tente de novo.");
    }

    /// <summary>
    /// Mercado Pago calls this for each payment event with <c>?data.id=…&amp;type=payment</c>. The body is not trusted: after the
    /// signature check the payment is read from the provider.
    /// </summary>
    [HttpPost("api/v1/webhooks/mercadopago")]
    [EndpointSummary("Mercado Pago webhook")]
    [EndpointDescription("Receives payment notifications from Mercado Pago. Not for the app: protected by the x-signature header. The real status is read from the provider; unknown payments and events are acknowledged with 200 so it does not retry.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Webhook(
        [FromQuery(Name = "data.id")] string? dataId, [FromQuery] string? type, CancellationToken cancellationToken)
    {
        if (!_authenticator.IsValid(Request.Headers["x-signature"].FirstOrDefault(), Request.Headers["x-request-id"].FirstOrDefault(), dataId))
        {
            return Unauthorized();
        }

        if (string.Equals(type, "payment", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(dataId))
        {
            await _payments.ReconcileAsync(dataId, cancellationToken);
        }

        return Ok();
    }
}
