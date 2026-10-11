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
}
