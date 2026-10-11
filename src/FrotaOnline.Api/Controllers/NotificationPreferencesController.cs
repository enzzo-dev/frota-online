using FrotaOnline.Application.Notifications;
using FrotaOnline.Application.Notifications.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/notifications/preferences")]
[Authorize]
[Produces("application/json")]
public sealed class NotificationPreferencesController : ApiControllerBase
{
    private readonly INotificationPreferenceService _preferences;

    public NotificationPreferencesController(INotificationPreferenceService preferences) => _preferences = preferences;

    [HttpGet]
    [EndpointSummary("My notification settings")]
    [EndpointDescription("Two switches, both on by default. newFreightAlerts: a driver is told when a new freight that fits one of their vehicles opens. pushEnabled: master switch for pushes — off, the inbox still fills but the phone stays quiet.")]
    [ProducesResponseType<NotificationPreferencesResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Handle(await _preferences.GetAsync(CurrentUserId, cancellationToken), Ok);

    [HttpPut]
    [EndpointSummary("Change my notification settings")]
    [EndpointDescription("Send only the switch that changes, e.g. {\"newFreightAlerts\": false} stops the new-freight alerts and keeps every other push. {\"pushEnabled\": false} silences all pushes. A field left out keeps its value. Returns the settings after the change.")]
    [ProducesResponseType<NotificationPreferencesResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken) =>
        Handle(await _preferences.UpdateAsync(CurrentUserId, request, cancellationToken), Ok);
}
