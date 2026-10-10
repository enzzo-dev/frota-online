using FrotaOnline.Application.Deliveries;
using FrotaOnline.Application.Deliveries.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/admin/drivers/{driverId:guid}/fees")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public sealed class AdminFeesController : ApiControllerBase
{
    private readonly IFeeService _fees;

    public AdminFeesController(IFeeService fees) => _fees = fees;

    [HttpGet]
    [EndpointSummary("Fee statement of a driver")]
    [ProducesResponseType<FeeStatementResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid driverId, CancellationToken cancellationToken) =>
        Handle(await _fees.StatementForAdminAsync(driverId, cancellationToken), Ok);

    [HttpPost("settle")]
    [EndpointSummary("Record a fee payment from the driver")]
    [EndpointDescription("The amount cannot be higher than the open balance. Returns the updated statement.")]
    [ProducesResponseType<FeeStatementResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Settle(
        Guid driverId, [FromBody] SettleFeeRequest request, CancellationToken cancellationToken) =>
        Handle(await _fees.SettleAsync(driverId, request, cancellationToken), Ok);
}
