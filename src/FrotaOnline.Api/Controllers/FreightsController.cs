using FrotaOnline.Application.Marketplace;
using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/freights")]
[Authorize(Roles = "Customer")]
[Produces("application/json")]
public sealed class FreightsController : ApiControllerBase
{
    private readonly IFreightService _freights;
    private readonly IProposalSelectionService _selection;

    public FreightsController(IFreightService freights, IProposalSelectionService selection)
    {
        _freights = freights;
        _selection = selection;
    }

    [HttpPost]
    [EndpointSummary("Request a freight")]
    [EndpointDescription("Needs every active term accepted and two different saved addresses. Leave scheduledPickupAt empty for same-day (5 minute window); a future date, in UTC (ending in Z), gives a window of up to 24 hours.")]
    [ProducesResponseType<FreightResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(CreateFreightRequest request, CancellationToken cancellationToken) =>
        Handle(await _freights.CreateAsync(CurrentUserId, request, cancellationToken),
            freight => StatusCode(StatusCodes.Status201Created, freight));

    [HttpGet]
    [EndpointSummary("Freights requested by the customer")]
    [EndpointDescription("Newest first. Filter with ?status=Open, Approved, Expired or Cancelled.")]
    [ProducesResponseType<IReadOnlyList<FreightResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] FreightStatus? status, CancellationToken cancellationToken) =>
        Handle(await _freights.ListAsync(CurrentUserId, status, cancellationToken), Ok);

    [HttpGet("{freightId:guid}")]
    [EndpointSummary("One freight with its pending proposal count")]
    [ProducesResponseType<FreightResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid freightId, CancellationToken cancellationToken) =>
        Handle(await _freights.GetAsync(CurrentUserId, freightId, cancellationToken), Ok);

    [HttpPost("{freightId:guid}/cancel")]
    [EndpointSummary("Cancel an open freight")]
    [EndpointDescription("Pending proposals are rejected.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid freightId, CancellationToken cancellationToken) =>
        HandleEmpty(await _freights.CancelAsync(CurrentUserId, freightId, cancellationToken), NoContent);

    [HttpGet("{freightId:guid}/proposals")]
    [EndpointSummary("Proposals for a freight, cheapest first")]
    [EndpointDescription("Only pending proposals still inside the window. Rank 1 is the lowest price; ties go to the earliest proposal.")]
    [ProducesResponseType<IReadOnlyList<RankedProposalResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListProposals(Guid freightId, CancellationToken cancellationToken) =>
        Handle(await _selection.ListRankedAsync(CurrentUserId, freightId, cancellationToken), Ok);

    [HttpPost("{freightId:guid}/proposals/{proposalId:guid}/approve")]
    [EndpointSummary("Approve a proposal")]
    [EndpointDescription("Creates the delivery with the agreed price and the R$ 4,90 platform fee, reserves the driver's agenda, rejects the other proposals and closes the freight. Must happen inside the freight's window.")]
    [ProducesResponseType<ApprovedDeliveryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(Guid freightId, Guid proposalId, CancellationToken cancellationToken) =>
        Handle(await _selection.ApproveAsync(CurrentUserId, freightId, proposalId, cancellationToken),
            delivery => StatusCode(StatusCodes.Status201Created, delivery));
}
