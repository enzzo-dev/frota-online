using FrotaOnline.Api.Extensions;
using FrotaOnline.Application.Marketplace;
using FrotaOnline.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

/// <summary>
/// Entry points for Cloud Scheduler. Not part of the public API (hidden from the docs): the only caller accepted
/// is the scheduler's service account, through a Google-signed OIDC token.
/// </summary>
[ApiExplorerSettings(IgnoreApi = true)]
[Route("internal/jobs")]
[Authorize(Policy = InternalJobsAuthentication.Policy)]
public sealed class InternalJobsController : ApiControllerBase
{
    private readonly IFreightExpirationService _expiration;
    private readonly INotificationDispatcher _dispatcher;

    public InternalJobsController(IFreightExpirationService expiration, INotificationDispatcher dispatcher)
    {
        _expiration = expiration;
        _dispatcher = dispatcher;
    }

    /// <summary>Expires freights and proposals past their window and tells the people involved.</summary>
    [HttpPost("expire")]
    public async Task<IActionResult> Expire(CancellationToken cancellationToken)
    {
        var (freights, proposals) = await _expiration.ExpireDueAsync(cancellationToken);
        return Ok(new { freights, proposals });
    }

    /// <summary>Sends one batch of pending pushes. Safe to call from several instances at once.</summary>
    [HttpPost("dispatch-notifications")]
    public async Task<IActionResult> DispatchNotifications(CancellationToken cancellationToken)
    {
        var settled = await _dispatcher.DispatchPendingAsync(cancellationToken);
        return Ok(new { settled });
    }
}
