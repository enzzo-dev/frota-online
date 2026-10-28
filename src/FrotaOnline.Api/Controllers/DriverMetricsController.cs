using FrotaOnline.Application.Marketplace;
using FrotaOnline.Application.Marketplace.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1")]
[Authorize(Roles = "Driver")]
[Produces("application/json")]
public sealed class DriverMetricsController : ApiControllerBase
{
    private readonly IDriverMetricsService _metrics;

    public DriverMetricsController(IDriverMetricsService metrics) => _metrics = metrics;

    [HttpPost("freights/{freightId:guid}/view")]
    [EndpointSummary("Register that the driver saw an open freight")]
    [EndpointDescription("Call it when the driver opens a freight from the open list. Approved drivers only. One row per driver and freight: the first call stores the first view, the next ones update the last view and the counter. A freight that is no longer open answers 204 without recording anything.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordView(Guid freightId, CancellationToken cancellationToken) =>
        HandleEmpty(await _metrics.RecordFreightViewAsync(CurrentUserId, freightId, cancellationToken), NoContent);

    [HttpGet("driver/metrics")]
    [EndpointSummary("Performance metrics of the driver")]
    [EndpointDescription("freightsSeen (first view in the period), freightsMissed (seen freights that were taken by another driver or expired without a proposal from this driver), proposalsSent, proposalsAccepted, proposalsRejected, proposalsExpired, acceptanceRate (0 to 1, accepted over sent), deliveriesCompleted and deliveriesCancelled. Optional ?from= and ?to= (ISO 8601, UTC) filter each metric by its own date: first view, proposal creation, delivery or cancellation time.")]
    [ProducesResponseType<DriverMetricsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken) =>
        Handle(await _metrics.GetAsync(CurrentUserId, from, to, cancellationToken), Ok);
}
