using FrotaOnline.Application.Payments;
using FrotaOnline.Application.Payments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/driver/wallet-account")]
[Authorize(Roles = "Driver")]
[Produces("application/json")]
public sealed class DriverWalletAccountController : ApiControllerBase
{
    private readonly IWalletAccountService _wallet;

    public DriverWalletAccountController(IWalletAccountService wallet) => _wallet = wallet;

    [HttpGet]
    [EndpointSummary("Is my Mercado Pago account connected?")]
    [EndpointDescription("Needed for customers to pay this driver with Apple Pay. Pix and card payments use the payout account instead.")]
    [ProducesResponseType<WalletAccountResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Handle(await _wallet.GetAsync(CurrentUserId, cancellationToken), Ok);

    [HttpPost("authorization")]
    [EndpointSummary("Start connecting my Mercado Pago account")]
    [EndpointDescription("Returns authorizationUrl: open it in the browser; the driver signs in to Mercado Pago and allows the platform to receive payments for them. Mercado Pago then redirects to the callback, which saves the connection. The link is valid for 15 minutes.")]
    [ProducesResponseType<WalletAuthorizationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Start(CancellationToken cancellationToken) =>
        Handle(await _wallet.StartAuthorizationAsync(CurrentUserId, cancellationToken), Ok);

    [HttpDelete]
    [EndpointSummary("Disconnect my Mercado Pago account")]
    [EndpointDescription("Deletes the stored tokens. Wallet payments stop being offered for this driver.")]
    [ProducesResponseType<WalletAccountResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Disconnect(CancellationToken cancellationToken) =>
        Handle(await _wallet.DisconnectAsync(CurrentUserId, cancellationToken), Ok);
}
