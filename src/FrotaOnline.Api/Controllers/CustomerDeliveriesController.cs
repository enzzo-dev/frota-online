using FrotaOnline.Application.Deliveries;
using FrotaOnline.Application.Deliveries.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/deliveries")]
[Authorize(Roles = "Customer")]
[Produces("application/json")]
public sealed class CustomerDeliveriesController : ApiControllerBase
{
    private readonly ICustomerDeliveryService _deliveries;
    private readonly IDeliveryProgressService _progress;

    public CustomerDeliveriesController(ICustomerDeliveryService deliveries, IDeliveryProgressService progress)
    {
        _deliveries = deliveries;
        _progress = progress;
    }

    [HttpGet]
    [EndpointSummary("Deliveries of the customer")]
    [EndpointDescription("Newest first, with driver, vehicle, progress and payment status.")]
    [ProducesResponseType<IReadOnlyList<CustomerDeliverySummary>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Handle(await _deliveries.ListAsync(CurrentUserId, cancellationToken), Ok);

    [HttpGet("{deliveryId:guid}")]
    [EndpointSummary("Follow a delivery")]
    [EndpointDescription("Status, stops with completion times and the event timeline.")]
    [ProducesResponseType<CustomerDeliveryDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid deliveryId, CancellationToken cancellationToken) =>
        Handle(await _deliveries.GetAsync(CurrentUserId, deliveryId, cancellationToken), Ok);

    [HttpPost("{deliveryId:guid}/cancel")]
    [EndpointSummary("Cancel a delivery")]
    [EndpointDescription("Only before the cargo is picked up (Scheduled or ToPickup).")]
    [ProducesResponseType<DeliveryProgressResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        Guid deliveryId, [FromBody] CancelDeliveryRequest request, CancellationToken cancellationToken) =>
        Handle(await _progress.CancelAsCustomerAsync(CurrentUserId, deliveryId, request, cancellationToken), Ok);
}
