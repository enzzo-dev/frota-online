using FrotaOnline.Application.Marketplace;
using FrotaOnline.Application.Marketplace.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/driver")]
[Authorize(Roles = "Driver")]
[Produces("application/json")]
public sealed class DriverMarketplaceController : ApiControllerBase
{
    private readonly IDriverMarketplaceService _marketplace;

    public DriverMarketplaceController(IDriverMarketplaceService marketplace) => _marketplace = marketplace;

    [HttpGet("freights/open")]
    [EndpointSummary("Open freights that fit the driver's vehicles")]
    [EndpointDescription("Approved drivers only. Shows district and city, not the street, until the proposal is approved.")]
    [ProducesResponseType<IReadOnlyList<OpenFreightResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListOpen(CancellationToken cancellationToken) =>
        Handle(await _marketplace.ListOpenFreightsAsync(CurrentUserId, cancellationToken), Ok);

    [HttpPost("freights/{freightId:guid}/proposals")]
    [EndpointSummary("Send a proposal with your price")]
    [EndpointDescription("The vehicle must be active, of the type the freight needs and with enough capacity. One pending proposal per freight, and the driver's agenda must be free for the period.")]
    [ProducesResponseType<ProposalResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitProposal(
        Guid freightId, SubmitProposalRequest request, CancellationToken cancellationToken) =>
        Handle(await _marketplace.SubmitProposalAsync(CurrentUserId, freightId, request, cancellationToken),
            proposal => StatusCode(StatusCodes.Status201Created, proposal));

    [HttpGet("proposals")]
    [EndpointSummary("Proposals sent by the driver")]
    [ProducesResponseType<IReadOnlyList<ProposalResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListProposals(CancellationToken cancellationToken) =>
        Handle(await _marketplace.ListProposalsAsync(CurrentUserId, cancellationToken), Ok);

    [HttpPost("proposals/{proposalId:guid}/withdraw")]
    [EndpointSummary("Withdraw a pending proposal")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Withdraw(Guid proposalId, CancellationToken cancellationToken) =>
        HandleEmpty(await _marketplace.WithdrawProposalAsync(CurrentUserId, proposalId, cancellationToken), NoContent);
}
