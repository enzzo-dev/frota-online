using FrotaOnline.Application.Terms;
using FrotaOnline.Application.Terms.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/terms")]
[Produces("application/json")]
public sealed class TermsController : ApiControllerBase
{
    private readonly ITermService _terms;

    public TermsController(ITermService terms) => _terms = terms;

    [AllowAnonymous]
    [HttpGet("active")]
    [EndpointSummary("Current terms of use and privacy policy")]
    [EndpointDescription("Public. Show these before sign-up and send the chosen ids to POST /accept after login.")]
    [ProducesResponseType<IReadOnlyList<TermResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken) =>
        Handle(await _terms.GetActiveAsync(cancellationToken), Ok);

    [Authorize]
    [HttpGet("pending")]
    [EndpointSummary("Active terms the signed-in user has not accepted")]
    [ProducesResponseType<IReadOnlyList<TermResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken) =>
        Handle(await _terms.GetPendingAsync(CurrentUserId, cancellationToken), Ok);

    [Authorize]
    [HttpGet("consents")]
    [EndpointSummary("History of terms accepted by the signed-in user")]
    [ProducesResponseType<IReadOnlyList<ConsentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetConsents(CancellationToken cancellationToken) =>
        Handle(await _terms.GetConsentsAsync(CurrentUserId, cancellationToken), Ok);

    [Authorize]
    [HttpPost("accept")]
    [EndpointSummary("Accept a term")]
    [EndpointDescription("Records the acceptance with timestamp and IP. Accepting the same term twice returns the original acceptance.")]
    [ProducesResponseType<ConsentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Accept(AcceptTermRequest request, CancellationToken cancellationToken) =>
        Handle(await _terms.AcceptAsync(CurrentUserId, request, ClientIpAddress, cancellationToken), Ok);
}
