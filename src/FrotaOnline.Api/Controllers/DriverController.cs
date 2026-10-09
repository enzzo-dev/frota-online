using FrotaOnline.Application.Drivers;
using FrotaOnline.Application.Drivers.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/driver")]
[Authorize(Roles = "Driver")]
[Produces("application/json")]
public sealed class DriverController : ApiControllerBase
{
    private readonly IDriverProfileService _profile;
    private readonly IVehicleService _vehicles;
    private readonly IDriverDocumentService _documents;

    public DriverController(IDriverProfileService profile, IVehicleService vehicles, IDriverDocumentService documents)
    {
        _profile = profile;
        _vehicles = vehicles;
        _documents = documents;
    }

    [HttpGet("profile")]
    [EndpointSummary("Driver profile with status and submission checklist")]
    [EndpointDescription("The checklist shows what is still missing before POST /submit: accepted terms, an active vehicle and a document.")]
    [ProducesResponseType<DriverProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken) =>
        Handle(await _profile.GetAsync(CurrentUserId, cancellationToken), Ok);

    [HttpPut("profile")]
    [EndpointSummary("Create or update the driver profile")]
    [EndpointDescription("TaxId is a CPF or a CNPJ (letters allowed, as in the 2026 alphanumeric CNPJ). It is required for drivers and validated by its check digits before anything is saved; punctuation is optional. Editable only while Draft or Rejected.")]
    [ProducesResponseType<DriverProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpsertProfile(UpsertDriverProfileRequest request, CancellationToken cancellationToken) =>
        Handle(await _profile.UpsertAsync(CurrentUserId, request, cancellationToken), Ok);

    [HttpPost("submit")]
    [EndpointSummary("Send the profile to review")]
    [EndpointDescription("Fails with the list of missing items when the checklist is incomplete. Moves the status to PendingReview.")]
    [ProducesResponseType<DriverProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(CancellationToken cancellationToken) =>
        Handle(await _profile.SubmitForReviewAsync(CurrentUserId, cancellationToken), Ok);

    [HttpGet("vehicles")]
    [EndpointSummary("Active vehicles of the driver")]
    [ProducesResponseType<IReadOnlyList<VehicleResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListVehicles(CancellationToken cancellationToken) =>
        Handle(await _vehicles.ListAsync(CurrentUserId, cancellationToken), Ok);

    [HttpPost("vehicles")]
    [EndpointSummary("Register a vehicle")]
    [EndpointDescription("Plate accepts ABC1234 and Mercosul ABC1D23. Create the profile first.")]
    [ProducesResponseType<VehicleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterVehicle(RegisterVehicleRequest request, CancellationToken cancellationToken) =>
        Handle(await _vehicles.RegisterAsync(CurrentUserId, request, cancellationToken),
            vehicle => StatusCode(StatusCodes.Status201Created, vehicle));

    [HttpDelete("vehicles/{vehicleId:guid}")]
    [EndpointSummary("Deactivate a vehicle")]
    [EndpointDescription("An approved driver cannot deactivate the last active vehicle.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeactivateVehicle(Guid vehicleId, CancellationToken cancellationToken) =>
        HandleEmpty(await _vehicles.DeactivateAsync(CurrentUserId, vehicleId, cancellationToken), NoContent);

    [HttpGet("documents")]
    [EndpointSummary("Files the driver has registered")]
    [ProducesResponseType<IReadOnlyList<MediaFileResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDocuments(CancellationToken cancellationToken) =>
        Handle(await _documents.ListAsync(CurrentUserId, cancellationToken), Ok);

    [HttpPost("documents")]
    [EndpointSummary("Register an uploaded document or vehicle photo")]
    [EndpointDescription("Call after the file is in storage. The storage key must start with drivers/{your user id}/. JPEG, PNG or PDF, up to 10 MB.")]
    [ProducesResponseType<MediaFileResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterDocument(RegisterMediaRequest request, CancellationToken cancellationToken) =>
        Handle(await _documents.RegisterAsync(CurrentUserId, request, cancellationToken),
            file => StatusCode(StatusCodes.Status201Created, file));
}
