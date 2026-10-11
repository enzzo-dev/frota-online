using FrotaOnline.Application.Notifications;
using FrotaOnline.Application.Notifications.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/devices")]
[Authorize]
[Produces("application/json")]
public sealed class DevicesController : ApiControllerBase
{
    private readonly IDeviceService _devices;

    public DevicesController(IDeviceService devices) => _devices = devices;

    [HttpPut]
    [EndpointSummary("Register this phone for push notifications")]
    [EndpointDescription("Send the push token the app got from the push provider (Firebase) together with the platform (Android, Ios, Web). Call it after every login and whenever the provider issues a new token; calling it again with a known token is harmless. A token belongs to one person: if the same phone signs in as someone else, it moves to the new account.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(RegisterDeviceRequest request, CancellationToken cancellationToken) =>
        HandleEmpty(await _devices.RegisterAsync(CurrentUserId, request, cancellationToken), NoContent);

    [HttpDelete]
    [EndpointSummary("Stop push notifications on this phone")]
    [EndpointDescription("Call it on logout with the same token, so the next person using the phone does not receive this account's notifications. Always answers 204, even for an unknown token.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unregister([FromQuery] string? token = null, CancellationToken cancellationToken = default) =>
        HandleEmpty(await _devices.UnregisterAsync(CurrentUserId, token, cancellationToken), NoContent);
}
