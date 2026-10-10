using FrotaOnline.Application.Marketplace;
using FrotaOnline.Application.Marketplace.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/driver/deliveries")]
[Authorize(Roles = "Driver")]
[Produces("application/json")]
public sealed class DriverDeliveriesController : ApiControllerBase
{
    private readonly IDriverDeliveryService _deliveries;

    public DriverDeliveriesController(IDriverDeliveryService deliveries) => _deliveries = deliveries;

    [HttpGet]
    [EndpointSummary("Deliveries the driver won")]
    [EndpointDescription("Newest first, with how many stops are done.")]
    [ProducesResponseType<IReadOnlyList<DriverDeliverySummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Handle(await _deliveries.ListAsync(CurrentUserId, cancellationToken), Ok);

    [HttpGet("{deliveryId:guid}/route")]
    [EndpointSummary("The route of a delivery, with full addresses")]
    [EndpointDescription("view=Full returns every pickup and delivery of the run at once. view=Next returns only the stop to do now, so the app can show one stop after the other. Each stop lists its items.")]
    [ProducesResponseType<DeliveryRouteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoute(
        Guid deliveryId, [FromQuery] RouteView view = RouteView.Full, CancellationToken cancellationToken = default) =>
        Handle(await _deliveries.GetRouteAsync(CurrentUserId, deliveryId, view, cancellationToken), Ok);
}
