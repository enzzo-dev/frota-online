using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Abstractions.Users;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Marketplace.Dtos;
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
    private readonly IUserRepository _users;
    private readonly IDeliveryRepository _deliveries;
    private readonly IDriverAgendaRepository _agenda;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public ProposalSelectionService(
        IFreightRepository freights,
        IProposalRepository proposals,
        IDriverRepository drivers,
        IVehicleRepository vehicles,
        IUserRepository users,
        IDeliveryRepository deliveries,
        IDriverAgendaRepository agenda,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock)
    {
        _freights = freights;
        _proposals = proposals;
        _drivers = drivers;
        _vehicles = vehicles;
        _users = users;
        _deliveries = deliveries;
        _agenda = agenda;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<RankedProposalResponse>>> ListRankedAsync(
        Guid customerId, Guid freightId, CancellationToken cancellationToken)
    {
        var freight = await _freights.GetByIdAsync(freightId, cancellationToken);
        if (freight is null || freight.CustomerId != customerId)
        {
            return Result.Failure<IReadOnlyList<RankedProposalResponse>>(MarketplaceErrors.FreightNotFound);
        }

        var now = _clock.UtcNow;
        var proposals = (await _proposals.ListPendingByFreightAsync(freight.Id, cancellationToken))
            .Where(p => now < p.ExpiresAt)
            .OrderBy(p => p.Price)
            .ThenBy(p => p.CreatedAt)
            .ToList();

        if (proposals.Count == 0)
        {
            return Result.Success<IReadOnlyList<RankedProposalResponse>>(Array.Empty<RankedProposalResponse>());
        }

        var drivers = (await _drivers.ListByIdsAsync(proposals.Select(p => p.DriverProfileId).Distinct().ToList(), cancellationToken))
            .ToDictionary(d => d.Id);
        var users = (await _users.ListByIdsAsync(drivers.Values.Select(d => d.UserId).ToList(), cancellationToken))
            .ToDictionary(u => u.Id);
        var vehicles = (await _vehicles.ListByIdsAsync(proposals.Select(p => p.VehicleId).Distinct().ToList(), cancellationToken))
            .ToDictionary(v => v.Id);

        var ranked = new List<RankedProposalResponse>();
        foreach (var proposal in proposals)
        {
            if (!drivers.TryGetValue(proposal.DriverProfileId, out var driver)
                || !users.TryGetValue(driver.UserId, out var user)
                || !vehicles.TryGetValue(proposal.VehicleId, out var vehicle))
            {
                continue;
            }

            ranked.Add(new RankedProposalResponse(
                ranked.Count + 1, proposal.Id, proposal.Price, proposal.Message, user.FullName,
                vehicle.Type, vehicle.Model, proposal.ExpiresAt, proposal.CreatedAt));
        }

        return Result.Success<IReadOnlyList<RankedProposalResponse>>(ranked);
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
        foreach (var other in await _proposals.ListPendingByFreightAsync(freight.Id, cancellationToken))
        {
            if (other.Id != proposal.Id)
            {
                other.Reject();
            }
        }

        freight.Approve(proposal.Id, now);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ApprovedDeliveryResponse(
            delivery.Id, freight.Id, proposal.Id, driver.Id, delivery.AgreedPrice, delivery.PlatformFee,
            delivery.Status, startsAt, endsAt));
    }
}
