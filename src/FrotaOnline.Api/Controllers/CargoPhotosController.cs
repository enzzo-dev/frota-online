using FrotaOnline.Application.Marketplace;
using FrotaOnline.Application.Marketplace.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/freights/{freightId:guid}/cargo-photo")]
public sealed class CargoPhotosController : ApiControllerBase
{
    private readonly ICargoPhotoService _photos;

    public CargoPhotosController(ICargoPhotoService photos) => _photos = photos;

    [HttpPost]
    [Authorize(Roles = "Customer")]
    [Consumes("application/json")]
    [EndpointSummary("Attach a photo of the cargo")]
    [EndpointDescription("Optional. Send the image in base64 (PNG or JPEG, up to 2 MB; a data URL such as data:image/jpeg;base64,... is accepted). Only the freight's customer, while the freight is open. The API checks the image, compresses it and saves it in the database. One photo per freight: 409 cargo_photo.already_exists when there is one. The freight responses carry hasCargoPhoto.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Upload(Guid freightId, UploadCargoPhotoRequest request, CancellationToken cancellationToken) =>
        HandleEmpty(await _photos.UploadAsync(CurrentUserId, freightId, request, cancellationToken), NoContent);

    [HttpGet]
    [Authorize(Roles = "Customer,Driver")]
    [Produces("image/png", "image/jpeg")]
    [EndpointSummary("Get the photo of the cargo")]
    [EndpointDescription("Returns the image (image/jpeg or image/png). Allowed to the freight's customer, to an approved driver while the freight is open and to the driver of its delivery. 404 when there is no photo. Fetch it only when hasCargoPhoto is true.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid freightId, CancellationToken cancellationToken) =>
        Handle(await _photos.GetAsync(CurrentUserId, freightId, cancellationToken),
            image => File(image.Content, image.ContentType));
}
