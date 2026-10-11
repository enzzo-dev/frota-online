using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Notifications;

namespace FrotaOnline.Application.Marketplace;

public sealed class FreightExpirationService : IFreightExpirationService
{
    private readonly IFreightRepository _freights;
    private readonly IProposalRepository _proposals;
    private readonly IDriverRepository _drivers;
    private readonly INotificationPublisher _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public FreightExpirationService(
        IFreightRepository freights,
        IProposalRepository proposals,
        IDriverRepository drivers,
        INotificationPublisher notifications,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock)
    {
        _freights = freights;
        _proposals = proposals;
        _drivers = drivers;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>
    /// Two set-based UPDATE statements; nothing is loaded into the application except the ids needed to tell
    /// customers their freight expired and drivers their proposal expired.
    /// </summary>
    public async Task<(int Freights, int Proposals)> ExpireDueAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var freights = await _freights.ExpireDueAsync(now, cancellationToken);
        var proposals = await _proposals.ExpireDueAsync(now, cancellationToken);

        if (freights.Count + proposals.Count > 0)
        {
            var messages = freights
                .Select(f => NotificationMessages.FreightExpired(f.CustomerId, f.FreightId))
                .ToList();

            var userIds = await _drivers.ListUserIdsAsync(
                proposals.Select(p => p.DriverProfileId).Distinct().ToList(), cancellationToken);
            messages.AddRange(proposals
                .Where(p => userIds.ContainsKey(p.DriverProfileId))
                .Select(p => NotificationMessages.ProposalExpired(userIds[p.DriverProfileId], p.ProposalId)));

            await _notifications.PublishAsync(messages, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return (freights.Count, proposals.Count);
    }
}
