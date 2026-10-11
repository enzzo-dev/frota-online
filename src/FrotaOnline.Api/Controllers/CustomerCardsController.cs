using FrotaOnline.Application.Payments;
using FrotaOnline.Application.Payments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/customer/cards")]
[Authorize(Roles = "Customer")]
[Produces("application/json")]
public sealed class CustomerCardsController : ApiControllerBase
{
    private readonly ICardPaymentService _cards;

    public CustomerCardsController(ICardPaymentService cards) => _cards = cards;

    [HttpGet]
    [EndpointSummary("My saved cards")]
    [EndpointDescription("Brand and last four digits only. The number is never stored; each saved card is a token held by the payment provider. At most 5.")]
    [ProducesResponseType<IReadOnlyList<SavedCardResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Handle(await _cards.ListCardsAsync(CurrentUserId, cancellationToken), Ok);

    [HttpDelete("{cardId:guid}")]
    [EndpointSummary("Remove a saved card")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid cardId, CancellationToken cancellationToken) =>
        HandleEmpty(await _cards.RemoveCardAsync(CurrentUserId, cardId, cancellationToken), NoContent);
}
