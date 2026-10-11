using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Application.Notifications;
using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Marketplace;

public sealed class ProposalSelectionService : IProposalSelectionService
{
    private readonly IFreightRepository _freights;
    private readonly IProposalRepository _proposals;
    private readonly IDriverRepository _drivers;
    private readonly IVehicleRepository _vehicles;
    private readonly IFreightQueries _queries;
    private readonly IDeliveryRepository _deliveries;
    private readonly IDriverAgendaRepository _agenda;
    private readonly INotificationPublisher _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public ProposalSelectionService(
        IFreightRepository freights,
        IProposalRepository proposals,
        IDriverRepository drivers,
        IVehicleRepository vehicles,
        IFreightQueries queries,
        IDeliveryRepository deliveries,
        IDriverAgendaRepository agenda,
        INotificationPublisher notifications,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock)
    {
        _freights = freights;
        _proposals = proposals;
        _drivers = drivers;
        _vehicles = vehicles;
        _queries = queries;
        _deliveries = deliveries;
        _agenda = agenda;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<RankedProposalResponse>>> ListRankedAsync(
        Guid customerId, Guid freightId, CancellationToken cancellationToken)
    {
        var ranked = await _queries.ListRankedProposalsAsync(customerId, freightId, _clock.UtcNow, cancellationToken);
        return ranked is null
            ? Result.Failure<IReadOnlyList<RankedProposalResponse>>(MarketplaceErrors.FreightNotFound)
            : Result.Success(ranked);
    }

    public async Task<Result<ApprovedDeliveryResponse>> ApproveAsync(
        Guid customerId, Guid freightId, Guid proposalId, CancellationToken cancellationToken)
    {
        var freight = await _freights.GetByIdAsync(freightId, cancellationToken);
        if (freight is null || freight.CustomerId != customerId)
        {
            return Result.Failure<ApprovedDeliveryResponse>(MarketplaceErrors.FreightNotFound);
        }

        var now = _clock.UtcNow;
        if (freight.Status != FreightStatus.Open)
        {
            return Result.Failure<ApprovedDeliveryResponse>(MarketplaceErrors.FreightNotOpen);
        }

        if (!freight.CanReceiveProposals(now))
        {
            return Result.Failure<ApprovedDeliveryResponse>(MarketplaceErrors.WindowClosed);
        }

        var proposal = await _proposals.GetByIdAsync(proposalId, cancellationToken);
        if (proposal is null || proposal.FreightRequestId != freight.Id)
        {
            return Result.Failure<ApprovedDeliveryResponse>(MarketplaceErrors.ProposalNotFound);
        }

        if (proposal.Status != ProposalStatus.Pending || now >= proposal.ExpiresAt)
        {
            return Result.Failure<ApprovedDeliveryResponse>(MarketplaceErrors.ProposalNotPending);
        }

        var driver = await _drivers.GetByIdAsync(proposal.DriverProfileId, cancellationToken);
        if (driver is null || !driver.CanOperate)
        {
            return Result.Failure<ApprovedDeliveryResponse>(MarketplaceErrors.DriverUnavailable);
        }

        var vehicle = await _vehicles.GetByIdAsync(proposal.VehicleId, cancellationToken);
        if (vehicle is null || !vehicle.IsActive)
        {
            return Result.Failure<ApprovedDeliveryResponse>(MarketplaceErrors.VehicleUnavailable);
        }

        var startsAt = freight.ScheduledPickupAt ?? now;
        var endsAt = startsAt + DeliveryPlanning.AgendaWindow(freight.Stops.Count);
        if (await _agenda.HasConflictAsync(driver.Id, startsAt, endsAt, cancellationToken))
        {
            return Result.Failure<ApprovedDeliveryResponse>(MarketplaceErrors.DriverBusy);
        }

        var delivery = Delivery.Create(freight.Id, proposal.Id, freight.CustomerId, driver.Id, vehicle.Id, proposal.Price);
        await _deliveries.AddAsync(delivery, cancellationToken);
        await _deliveries.AddEventAsync(
            DeliveryEvent.Create(delivery.Id, DeliveryStatus.Scheduled, now, "Proposal approved by the customer.", null, null, null),
            cancellationToken);
        await _agenda.AddAsync(DriverAgendaSlot.Create(driver.Id, delivery.Id, startsAt, endsAt), cancellationToken);

        proposal.Accept();
        var messages = new List<NotificationMessage> { NotificationMessages.ProposalApproved(driver.UserId, delivery.Id) };

        var others = (await _proposals.ListPendingByFreightAsync(freight.Id, cancellationToken))
            .Where(p => p.Id != proposal.Id)
            .ToList();
        var userIds = await _drivers.ListUserIdsAsync(
            others.Select(p => p.DriverProfileId).Distinct().ToList(), cancellationToken);

        foreach (var other in others)
        {
            other.Reject();
            if (userIds.TryGetValue(other.DriverProfileId, out var otherUserId))
            {
                messages.Add(NotificationMessages.ProposalNotSelected(otherUserId, other.Id));
            }
        }

        await _notifications.PublishAsync(messages, cancellationToken);
        freight.Approve(proposal.Id, now);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ApprovedDeliveryResponse(
            delivery.Id, freight.Id, proposal.Id, driver.Id, delivery.AgreedPrice, delivery.PlatformFee,
            delivery.Status, startsAt, endsAt));
    }
}
