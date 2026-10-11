using FrotaOnline.Application.Addresses;
using FrotaOnline.Application.Addresses.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/addresses")]
[Authorize(Roles = "Customer")]
[Produces("application/json")]
public sealed class AddressesController : ApiControllerBase
{
    private readonly IAddressService _addresses;

    public AddressesController(IAddressService addresses) => _addresses = addresses;

    [HttpGet]
    [EndpointSummary("Addresses saved by the customer")]
    [ProducesResponseType<IReadOnlyList<AddressResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Handle(await _addresses.ListAsync(CurrentUserId, cancellationToken), Ok);

    [HttpPost]
    [EndpointSummary("Save an address")]
    [EndpointDescription("Use the returned id in the stops of a freight and in the route preview. State is the two-letter code and zipCode has 8 digits. latitude (-90 to 90) and longitude (-180 to 180) are optional and must be sent together (only one of them is rejected) with the real position of the address, for example from a public geocoding API the app calls on the typed address. Without them the address still works, but the route on the map cannot be drawn (route.missing_coordinates).")]
    [ProducesResponseType<AddressResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateAddressRequest request, CancellationToken cancellationToken) =>
        Handle(await _addresses.CreateAsync(CurrentUserId, request, cancellationToken),
            address => StatusCode(StatusCodes.Status201Created, address));

    [HttpPut("{addressId:guid}")]
    [EndpointSummary("Edit an address")]
    [EndpointDescription("Same body as saving one: every field is replaced, so send latitude and longitude again (together) or they are cleared. Only an address no freight ever used can be edited; otherwise 409 address.in_use and the app should save a new address.")]
    [ProducesResponseType<AddressResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid addressId, CreateAddressRequest request, CancellationToken cancellationToken) =>
        Handle(await _addresses.UpdateAsync(CurrentUserId, addressId, request, cancellationToken), Ok);

    [HttpDelete("{addressId:guid}")]
    [EndpointSummary("Delete an address")]
    [EndpointDescription("An address no freight ever used is removed. One used only by finished freights disappears from the list but is kept for their history. One used by an open freight or a delivery in progress is refused with 409 address.in_active_use: cancel that freight first.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid addressId, CancellationToken cancellationToken) =>
        HandleEmpty(await _addresses.DeleteAsync(CurrentUserId, addressId, cancellationToken), NoContent);
}
