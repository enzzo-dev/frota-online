using FrotaOnline.Application.Notifications;
using FrotaOnline.Application.Notifications.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/notifications")]
[Authorize]
[Produces("application/json")]
public sealed class NotificationsController : ApiControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications) => _notifications = notifications;

    [HttpGet]
    [EndpointSummary("My notifications")]
    [EndpointDescription("The inbox of the signed-in person (customer or driver), newest first. page starts at 1 and pageSize goes from 1 to 100 (default 20). unreadOnly=true hides what was already read. Each item has targetType (Freight, Proposal, Delivery, DriverProfile, Fee) and targetId so the app opens the right screen. The response also carries unreadCount for the badge and hasMore for infinite scroll.")]
    [ProducesResponseType<NotificationListResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Handle(await _notifications.ListAsync(CurrentUserId, unreadOnly, page, pageSize, cancellationToken), Ok);

    [HttpGet("unread-count")]
    [EndpointSummary("Unread notifications badge")]
    [EndpointDescription("Just the number of unread notifications, cheap enough to poll for the badge on the bell icon.")]
    [ProducesResponseType<UnreadCountResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken) =>
        Handle(await _notifications.CountUnreadAsync(CurrentUserId, cancellationToken), Ok);

    [HttpPost("{notificationId:guid}/read")]
    [EndpointSummary("Mark a notification as read")]
    [EndpointDescription("Idempotent: marking an already read notification keeps its original read time.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken) =>
        HandleEmpty(await _notifications.MarkReadAsync(CurrentUserId, notificationId, cancellationToken), NoContent);

    [HttpPost("read-all")]
    [EndpointSummary("Mark every notification as read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken) =>
        HandleEmpty(await _notifications.MarkAllReadAsync(CurrentUserId, cancellationToken), NoContent);
}
