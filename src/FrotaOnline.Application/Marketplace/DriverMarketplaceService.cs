using FluentValidation;
using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Drivers;
using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Application.Notifications;
using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Marketplace;

public sealed class DriverMarketplaceService : IDriverMarketplaceService
{
    private readonly IDriverRepository _drivers;
    private readonly IVehicleRepository _vehicles;
    private readonly IFreightRepository _freights;
    private readonly IProposalRepository _proposals;
    private readonly IFreightQueries _queries;
    private readonly IDriverAgendaRepository _agenda;
    private readonly INotificationPublisher _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<SubmitProposalRequest> _validator;

    public DriverMarketplaceService(
        IDriverRepository drivers,
        IVehicleRepository vehicles,
        IFreightRepository freights,
        IProposalRepository proposals,
        IFreightQueries queries,
        IDriverAgendaRepository agenda,
        INotificationPublisher notifications,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IValidator<SubmitProposalRequest> validator)
    {
        _drivers = drivers;
        _vehicles = vehicles;
        _freights = freights;
        _proposals = proposals;
        _queries = queries;
        _agenda = agenda;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _validator = validator;
    }

    public async Task<Result<IReadOnlyList<OpenFreightResponse>>> ListOpenFreightsAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<IReadOnlyList<OpenFreightResponse>>(DriverErrors.ProfileNotFound);
        }

        if (!profile.CanOperate)
        {
            return Result.Failure<IReadOnlyList<OpenFreightResponse>>(MarketplaceErrors.DriverNotApproved);
        }

        var vehicles = await _vehicles.ListActiveByDriverAsync(profile.Id, cancellationToken);
        var types = vehicles.Select(v => v.Type).Distinct().ToList();
        if (types.Count == 0)
        {
            return Result.Success<IReadOnlyList<OpenFreightResponse>>(Array.Empty<OpenFreightResponse>());
        }

        var response = await _queries.ListOpenAsync(_clock.UtcNow, types, cancellationToken);

        return Result.Success<IReadOnlyList<OpenFreightResponse>>(response);
    }

    public async Task<Result<ProposalResponse>> SubmitProposalAsync(
        Guid userId, Guid freightId, SubmitProposalRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ProposalResponse>(ValidationMapper.ToError(validation));
        }

        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<ProposalResponse>(DriverErrors.ProfileNotFound);
        }

        if (!profile.CanOperate)
        {
            return Result.Failure<ProposalResponse>(MarketplaceErrors.DriverNotApproved);
        }

        var now = _clock.UtcNow;
        var freight = await _freights.GetByIdAsync(freightId, cancellationToken);
        if (freight is null)
        {
            return Result.Failure<ProposalResponse>(MarketplaceErrors.FreightNotFound);
        }

        if (freight.Status != FreightStatus.Open)
        {
            return Result.Failure<ProposalResponse>(MarketplaceErrors.FreightNotOpen);
        }

        if (!freight.CanReceiveProposals(now))
        {
            return Result.Failure<ProposalResponse>(MarketplaceErrors.WindowClosed);
        }

        var vehicle = await _vehicles.GetByIdAsync(request.VehicleId, cancellationToken);
        if (vehicle is null || vehicle.DriverProfileId != profile.Id || !vehicle.IsActive)
        {
            return Result.Failure<ProposalResponse>(MarketplaceErrors.VehicleNotEligible("Choose one of your active vehicles."));
        }

        if (vehicle.Type != freight.RequiredVehicleType)
        {
            return Result.Failure<ProposalResponse>(MarketplaceErrors.VehicleNotEligible(
                $"This freight needs a {freight.RequiredVehicleType}."));
        }

        if (vehicle.CapacityKg < freight.CargoWeightKg)
        {
            return Result.Failure<ProposalResponse>(MarketplaceErrors.VehicleNotEligible(
                "The vehicle capacity is lower than the cargo weight."));
        }

        if (await _proposals.HasPendingAsync(freight.Id, profile.Id, cancellationToken))
        {
            return Result.Failure<ProposalResponse>(MarketplaceErrors.AlreadyProposed);
        }

        var start = freight.ScheduledPickupAt ?? now;
        if (await _agenda.HasConflictAsync(profile.Id, start, start + DeliveryPlanning.AgendaWindow(freight.Stops.Count), cancellationToken))
        {
            return Result.Failure<ProposalResponse>(MarketplaceErrors.DriverBusy);
        }

        var proposal = Proposal.Create(
            freight.Id, profile.Id, vehicle.Id, request.Price, request.Message, freight.ProposalDeadline);

        await _proposals.AddAsync(proposal, cancellationToken);
        await _notifications.PublishAsync(
            NotificationMessages.ProposalReceived(freight.CustomerId, freight.Id, request.Price), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FreightMapper.ToResponse(proposal));
    }

    public async Task<Result<IReadOnlyList<ProposalResponse>>> ListProposalsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<IReadOnlyList<ProposalResponse>>(DriverErrors.ProfileNotFound);
        }

        var proposals = await _proposals.ListByDriverAsync(profile.Id, cancellationToken);
        return Result.Success<IReadOnlyList<ProposalResponse>>(proposals.Select(FreightMapper.ToResponse).ToList());
    }

    public async Task<Result> WithdrawProposalAsync(Guid userId, Guid proposalId, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure(DriverErrors.ProfileNotFound);
        }

        var proposal = await _proposals.GetByIdAsync(proposalId, cancellationToken);
        if (proposal is null || proposal.DriverProfileId != profile.Id)
        {
            return Result.Failure(MarketplaceErrors.ProposalNotFound);
        }

        if (proposal.Status != ProposalStatus.Pending)
        {
            return Result.Failure(MarketplaceErrors.ProposalNotPending);
        }

        proposal.Withdraw();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
