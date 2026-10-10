using FrotaOnline.Application.Deliveries;
using FrotaOnline.Application.Deliveries.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/driver/fees")]
[Authorize(Roles = "Driver")]
[Produces("application/json")]
public sealed class DriverFeesController : ApiControllerBase
{
    private readonly IFeeService _fees;

    public DriverFeesController(IFeeService fees) => _fees = fees;

    [HttpGet]
    [EndpointSummary("What the driver owes the platform")]
    [EndpointDescription("Balance is the fees accrued by delivered freights minus what the driver already settled. Entries are the 200 most recent.")]
    [ProducesResponseType<FeeStatementResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Handle(await _fees.StatementForDriverAsync(CurrentUserId, cancellationToken), Ok);
}
