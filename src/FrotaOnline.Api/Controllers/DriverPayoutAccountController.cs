using FrotaOnline.Application.Payments;
using FrotaOnline.Application.Payments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/driver/payout-account")]
[Authorize(Roles = "Driver")]
[Produces("application/json")]
public sealed class DriverPayoutAccountController : ApiControllerBase
{
    private readonly IPayoutAccountService _payout;

    public DriverPayoutAccountController(IPayoutAccountService payout) => _payout = payout;

    [HttpGet]
    [EndpointSummary("My payout account")]
    [EndpointDescription("The driver's own account at the payment provider (Asaas), where their share of a Pix paid in the app is sent. configured is false until the driver links one.")]
    [ProducesResponseType<PayoutAccountResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Handle(await _payout.GetAsync(CurrentUserId, cancellationToken), Ok);

    [HttpPut]
    [EndpointSummary("Link my payout account")]
    [EndpointDescription("Body: { \"walletId\": \"<uuid>\" }, the wallet id of the driver's Asaas account. Without it a customer cannot pay this driver by Pix in the app (paying the driver directly still works). One account can belong to only one driver.")]
    [ProducesResponseType<PayoutAccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] UpdatePayoutAccountRequest request, CancellationToken cancellationToken) =>
        Handle(await _payout.UpdateAsync(CurrentUserId, request, cancellationToken), Ok);
}
