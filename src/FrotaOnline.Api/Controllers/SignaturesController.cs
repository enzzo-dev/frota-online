using FrotaOnline.Application.Signatures;
using FrotaOnline.Application.Signatures.Dtos;
using FrotaOnline.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/signatures")]
[Authorize(Roles = "Customer,Driver")]
[Produces("application/json")]
public sealed class SignaturesController : ApiControllerBase
{
    private readonly ISignatureService _signatures;

    public SignaturesController(ISignatureService signatures) => _signatures = signatures;

    [HttpPost]
    [EndpointSummary("Collect a signature")]
    [EndpointDescription("Send the type (Customer or Driver), the freightId and the image in base64 (PNG or JPEG, up to 2 MB; a data URL such as data:image/png;base64,... is accepted). Driver is the signature of the freight's driver. Customer is the signature of its customer, who can sign it themselves or have the driver collect it on the driver's phone. The API checks the image, compresses it and saves it in the database with the customer, the driver and the freight of the delivery. One signature per type per freight. Answers 409 when it was already collected or the delivery was cancelled.")]
    [ProducesResponseType<SignatureResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterSignatureRequest request, CancellationToken cancellationToken) =>
        Handle(await _signatures.RegisterAsync(CurrentUserId, request, cancellationToken),
            signature => StatusCode(StatusCodes.Status201Created, signature));

    [HttpGet("{freightId:guid}/{type}")]
    [EndpointSummary("Get a signature image")]
    [EndpointDescription("Returns the signature image (image/png or image/jpeg) of the freight, for its customer or its driver. Answers 404 when it was not collected yet.")]
    [Produces("image/png", "image/jpeg")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(Guid freightId, SignatureType type, CancellationToken cancellationToken) =>
        Handle(await _signatures.GetImageAsync(CurrentUserId, freightId, type, cancellationToken),
            image => File(image.Content, image.ContentType));
}
