using FrotaOnline.Application.Deliveries;
using FrotaOnline.Application.Deliveries.Dtos;
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
    private readonly IDeliveryProgressService _progress;
    private readonly IDeliveryPaymentService _payments;

    public DriverDeliveriesController(
        IDriverDeliveryService deliveries, IDeliveryProgressService progress, IDeliveryPaymentService payments)
    {
        _deliveries = deliveries;
        _progress = progress;
        _payments = payments;
    }

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

    [HttpPost("{deliveryId:guid}/start")]
    [EndpointSummary("Leave for the first pickup")]
    [EndpointDescription("Scheduled becomes ToPickup.")]
    [ProducesResponseType<DeliveryProgressResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Start(Guid deliveryId, CancellationToken cancellationToken) =>
        Handle(await _progress.StartAsync(CurrentUserId, deliveryId, cancellationToken), Ok);

    [HttpPost("{deliveryId:guid}/stops/{stopId:guid}/complete")]
    [EndpointSummary("Complete a stop")]
    [EndpointDescription("Stops must be completed in order. The first pickup moves the delivery to PickedUp, later stops keep it InTransit and the last stop marks it Delivered, accrues the R$ 4.90 fee and frees the agenda. Note, coordinates and a DeliveryProof file (registered through the media endpoint) are optional.")]
    [ProducesResponseType<DeliveryProgressResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CompleteStop(
        Guid deliveryId, Guid stopId, [FromBody] CompleteStopRequest request, CancellationToken cancellationToken) =>
        Handle(await _progress.CompleteStopAsync(CurrentUserId, deliveryId, stopId, request, cancellationToken), Ok);

    [HttpPost("{deliveryId:guid}/incident")]
    [EndpointSummary("Report an incident")]
    [EndpointDescription("Allowed while ToPickup, PickedUp or InTransit. After an incident the delivery can only be cancelled.")]
    [ProducesResponseType<DeliveryProgressResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReportIncident(
        Guid deliveryId, [FromBody] ReportIncidentRequest request, CancellationToken cancellationToken) =>
        Handle(await _progress.ReportIncidentAsync(CurrentUserId, deliveryId, request, cancellationToken), Ok);

    [HttpPost("{deliveryId:guid}/cancel")]
    [EndpointSummary("Cancel as the driver")]
    [EndpointDescription("Allowed while Scheduled, ToPickup or after an incident. Frees the agenda.")]
    [ProducesResponseType<DeliveryProgressResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        Guid deliveryId, [FromBody] CancelDeliveryRequest request, CancellationToken cancellationToken) =>
        Handle(await _progress.CancelAsDriverAsync(CurrentUserId, deliveryId, request, cancellationToken), Ok);

    [HttpPost("{deliveryId:guid}/payment")]
    [EndpointSummary("Confirm that the customer paid you")]
    [EndpointDescription("Only after Delivered, once per delivery. The payment happens outside the app; the platform never holds the money.")]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmPayment(
        Guid deliveryId, [FromBody] ConfirmPaymentRequest request, CancellationToken cancellationToken) =>
        Handle(await _payments.ConfirmAsync(CurrentUserId, deliveryId, request, cancellationToken), Ok);
}
