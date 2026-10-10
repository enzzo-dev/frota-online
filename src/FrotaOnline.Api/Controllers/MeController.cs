using FrotaOnline.Application.Privacy;
using FrotaOnline.Application.Privacy.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

/// <summary>Rights of the data subject under the LGPD (Law 13.709/2018, art. 18).</summary>
[Route("api/v1/me")]
[Authorize]
[Produces("application/json")]
public sealed class MeController : ApiControllerBase
{
    private readonly IPrivacyService _privacy;

    public MeController(IPrivacyService privacy) => _privacy = privacy;

    [HttpGet("data")]
    [EndpointSummary("Export all personal data held about the signed-in user")]
    [EndpointDescription("Access and portability (LGPD art. 18, II and V). Returns account, driver data, vehicles, addresses, files, consents, freights, proposals and deliveries as JSON.")]
    [ProducesResponseType<PersonalDataExport>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportData(CancellationToken cancellationToken) =>
        Handle(await _privacy.ExportAsync(CurrentUserId, cancellationToken), Ok);

    [HttpPost("deletion")]
    [EndpointSummary("Delete the signed-in user's account and personal data")]
    [EndpointDescription("Elimination of personal data (LGPD art. 18, VI). Requires the password and confirm=true. Identity, contact data, documents, addresses and files are erased or anonymized; freight, delivery and payment history stays without identifying anyone. Open freights are cancelled and pending proposals closed. Fails with 409 while a delivery is in progress. Administrators cannot use it. Existing access tokens stay valid until they expire, so the client should discard them.")]
    [ProducesResponseType<AccountDeletionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAccount(DeleteAccountRequest request, CancellationToken cancellationToken) =>
        Handle(await _privacy.DeleteAccountAsync(CurrentUserId, request, cancellationToken), Ok);
}
