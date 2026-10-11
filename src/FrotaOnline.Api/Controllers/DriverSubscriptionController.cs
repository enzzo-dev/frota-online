using FrotaOnline.Application.Payments;
using FrotaOnline.Application.Payments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/driver/subscription")]
[Authorize(Roles = "Driver")]
[Produces("application/json")]
public sealed class DriverSubscriptionController : ApiControllerBase
{
    private readonly ISubscriptionService _subscriptions;

    public DriverSubscriptionController(ISubscriptionService subscriptions) => _subscriptions = subscriptions;

    [HttpGet]
    [EndpointSummary("Plans and my subscription")]
    [EndpointDescription("The plans on offer (Basic R$ 79,90 up to 3 vehicles; Full R$ 99,90 with no limit) and, if the driver has one, the current subscription. hasAccess is true while the driver is inside a period they paid for. paymentUrl is only present while the first charge is unpaid.")]
    [ProducesResponseType<SubscriptionOverview>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Handle(await _subscriptions.GetAsync(CurrentUserId, cancellationToken), Ok);

    [HttpPost]
    [EndpointSummary("Subscribe to a plan")]
    [EndpointDescription("Body: { \"plan\": \"Basic\" | \"Full\" }. Approved drivers only. Creates the monthly charge at the payment provider and returns paymentUrl: open it so the driver pays by Pix, boleto or card (card data never passes through this API). The plan turns Active when the provider confirms the payment, and each paid month extends it by one month. Asking again for the same plan before paying returns the same link. 409 when a plan is already active, or when the Basic plan is chosen with more than 3 active vehicles; 503 when the provider is unavailable.")]
    [ProducesResponseType<SubscriptionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request, CancellationToken cancellationToken) =>
        Handle(await _subscriptions.SubscribeAsync(CurrentUserId, request, cancellationToken), r => StatusCode(StatusCodes.Status201Created, r));

    [HttpDelete]
    [EndpointSummary("Cancel my subscription")]
    [EndpointDescription("Stops future charges. The driver keeps the access already paid for until the end of the current period (status Cancelled, hasAccess true until endsAt).")]
    [ProducesResponseType<SubscriptionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(CancellationToken cancellationToken) =>
        Handle(await _subscriptions.CancelAsync(CurrentUserId, cancellationToken), Ok);
}
