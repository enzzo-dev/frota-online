using FrotaOnline.Application.Signatures;
using FrotaOnline.Application.Signatures.Dtos;
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

    [HttpPost("upload-url")]
    [EndpointSummary("Get a signed URL to upload a signature")]
    [EndpointDescription("Step 1 of collecting a signature. Send the type (Customer or Driver), the freightId and the content type (image/png or image/jpeg). Driver is the signature of the freight's driver. Customer is the signature of its customer, who can sign it themselves or have the driver collect it on the driver's phone. The response has the storagePath, the uploadUrl and the headers: upload the image with HTTP PUT to uploadUrl sending exactly those headers, within 15 minutes and up to 2 MB. Then call POST /signatures with the same storagePath. The file goes straight to storage, not through the API. Answers 409 when that signature was already collected or the delivery was cancelled, and 409 storage.not_configured in environments without storage.")]
    [ProducesResponseType<SignatureUploadUrlResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUploadUrl(CreateSignatureUploadUrlRequest request, CancellationToken cancellationToken) =>
        Handle(await _signatures.CreateUploadUrlAsync(CurrentUserId, request, cancellationToken), Ok);

    [HttpPost]
    [EndpointSummary("Register a collected signature")]
    [EndpointDescription("Step 2. Send the same type and freightId plus the storagePath from the upload-url response. The API checks that the file reached storage (image/png or image/jpeg, up to 2 MB) and saves the signature with the customer, the driver and the freight of the delivery. One signature per type per freight.")]
    [ProducesResponseType<SignatureResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterSignatureRequest request, CancellationToken cancellationToken) =>
        Handle(await _signatures.RegisterAsync(CurrentUserId, request, cancellationToken),
            signature => StatusCode(StatusCodes.Status201Created, signature));
}
