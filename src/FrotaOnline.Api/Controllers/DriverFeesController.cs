using FrotaOnline.Application.Deliveries;
using FrotaOnline.Application.Deliveries.Dtos;
using FrotaOnline.Application.Payments;
using FrotaOnline.Application.Payments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/driver/fees")]
[Authorize(Roles = "Driver")]
[Produces("application/json")]
public sealed class DriverFeesController : ApiControllerBase
{
    private readonly IFeeService _fees;
    private readonly IFeeChargeService _charges;

    public DriverFeesController(IFeeService fees, IFeeChargeService charges)
    {
        _fees = fees;
        _charges = charges;
    }

    [HttpGet]
    [EndpointSummary("What the driver owes the platform")]
    [EndpointDescription("Balance is the fees accrued by delivered freights minus what the driver already settled. Entries are the 200 most recent.")]
    [ProducesResponseType<FeeStatementResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Handle(await _fees.StatementForDriverAsync(CurrentUserId, cancellationToken), Ok);

    [HttpPost("pix")]
    [EndpointSummary("Pay my platform fees by Pix")]
    [EndpointDescription("Issues a Pix for the whole open balance (freights paid outside the app). Show pixPayload (copia e cola) and pixQrCode (base64 PNG). When the Pix is paid the balance is settled automatically and a FeeSettled notification arrives; no admin step. Asking again returns the same Pix while it is valid and the balance is unchanged. The provider's minimum charge is R$ 5,00: below that the balance waits for the next freights (409 fee.below_minimum). 409 fee.nothing_to_charge when nothing is owed.")]
    [ProducesResponseType<FeeChargeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Pix(CancellationToken cancellationToken) =>
        Handle(await _charges.CreateAsync(CurrentUserId, cancellationToken), Ok);
}
