using FrotaOnline.Application.Terms;
using FrotaOnline.Application.Terms.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/admin/terms")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public sealed class AdminTermsController : ApiControllerBase
{
    private readonly ITermService _terms;

    public AdminTermsController(ITermService terms) => _terms = terms;

    [HttpPost]
    [EndpointSummary("Publish a new version of a term")]
    [EndpointDescription("Deactivates the previous active version of the same type. Users must accept the new version again.")]
    [ProducesResponseType<TermResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Publish(PublishTermRequest request, CancellationToken cancellationToken) =>
        Handle(await _terms.PublishAsync(request, cancellationToken), term => StatusCode(StatusCodes.Status201Created, term));
}
