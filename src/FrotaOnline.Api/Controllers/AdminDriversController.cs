using FrotaOnline.Application.Admin;
using FrotaOnline.Application.Admin.Dtos;
using FrotaOnline.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/admin/drivers")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public sealed class AdminDriversController : ApiControllerBase
{
    private readonly IDriverReviewService _review;

    public AdminDriversController(IDriverReviewService review) => _review = review;

    [HttpGet]
    [EndpointSummary("Driver review queue")]
    [EndpointDescription("Defaults to PendingReview, oldest first. Pass status=Approved or Rejected to browse the others.")]
    [ProducesResponseType<IReadOnlyList<DriverReviewSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List([FromQuery] DriverStatus status = DriverStatus.PendingReview, CancellationToken cancellationToken = default) =>
        Handle(await _review.ListAsync(status, cancellationToken), Ok);

    [HttpGet("{driverId:guid}")]
    [EndpointSummary("Full driver file for review")]
    [EndpointDescription("Profile, contact data, active vehicles and registered files (storage keys).")]
    [ProducesResponseType<DriverReviewDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid driverId, CancellationToken cancellationToken) =>
        Handle(await _review.GetAsync(driverId, cancellationToken), Ok);

    [HttpPost("{driverId:guid}/approve")]
    [EndpointSummary("Approve a driver")]
    [EndpointDescription("Only drivers in PendingReview. Sends the approval e-mail.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(Guid driverId, CancellationToken cancellationToken) =>
        HandleEmpty(await _review.ApproveAsync(CurrentUserId, driverId, cancellationToken), NoContent);

    [HttpPost("{driverId:guid}/reject")]
    [EndpointSummary("Reject a driver with a reason")]
    [EndpointDescription("The reason is stored and e-mailed. The driver can fix the data and submit again.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(Guid driverId, RejectDriverRequest request, CancellationToken cancellationToken) =>
        HandleEmpty(await _review.RejectAsync(CurrentUserId, driverId, request, cancellationToken), NoContent);
}
