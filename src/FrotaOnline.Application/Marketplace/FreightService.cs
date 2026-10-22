using FluentValidation;
using FrotaOnline.Application.Abstractions.Addresses;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Addresses;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Routing;
using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Application.Notifications;
using FrotaOnline.Application.Terms;
using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Marketplace;

public sealed class FreightService : IFreightService
{
    /// <summary>Most drivers told about one freight. Past this the alert is a flood, and the list screen is still there.</summary>
    public const int MaxAlertedDrivers = 500;

    private readonly IFreightRepository _freights;
    private readonly IProposalRepository _proposals;
    private readonly IAddressRepository _addresses;
    private readonly IFreightQueries _queries;
    private readonly ITermAcceptanceChecker _terms;
    private readonly IRoutePlanner _routes;
    private readonly IMatchingDriverQueries _matching;
    private readonly INotificationPublisher _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<CreateFreightRequest> _validator;

    public FreightService(
        IFreightRepository freights,
        IProposalRepository proposals,
        IAddressRepository addresses,
        IFreightQueries queries,
        ITermAcceptanceChecker terms,
        IRoutePlanner routes,
        IMatchingDriverQueries matching,
        INotificationPublisher notifications,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IValidator<CreateFreightRequest> validator)
    {
        _freights = freights;
        _proposals = proposals;
        _addresses = addresses;
        _queries = queries;
        _terms = terms;
        _routes = routes;
        _matching = matching;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _validator = validator;
    }

    public async Task<Result<FreightResponse>> CreateAsync(
        Guid customerId, CreateFreightRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<FreightResponse>(ValidationMapper.ToError(validation));
        }

        var now = _clock.UtcNow;
        if (request.ScheduledPickupAt is not null && request.ScheduledPickupAt.Value.ToUniversalTime() <= now)
        {
            return Result.Failure<FreightResponse>(MarketplaceErrors.ScheduledInPast);
        }

        if (!await _terms.HasAcceptedAllActiveAsync(customerId, cancellationToken))
        {
            return Result.Failure<FreightResponse>(MarketplaceErrors.TermsNotAccepted);
        }

        var addressIds = request.Stops.Select(s => s.AddressId).Distinct().ToList();
        var found = (await _addresses.ListByIdsAsync(addressIds, cancellationToken)).ToDictionary(a => a.Id);
        if (addressIds.Any(id => !found.TryGetValue(id, out var address) || address.OwnerUserId != customerId || address.IsDeleted))
        {
            return Result.Failure<FreightResponse>(AddressErrors.NotFound);
        }

        var drafts = request.Stops
            .Select(s => new FreightStopDraft(
                s.Type,
                s.AddressId,
                s.Note,
                (s.Items ?? Array.Empty<CreateFreightItemRequest>())
                    .Select(i => new FreightItemDraft(i.Description, i.Quantity))
                    .ToList()))
            .ToList();

        var freight = FreightRequest.Create(
            customerId, drafts, request.CargoDescription, request.CargoWeightKg,
            request.RequiredVehicleType, request.ScheduledPickupAt?.ToUniversalTime(), now);

        // Distance and time for the drivers' list. Best effort: a slow or missing routing engine never blocks the request.
        var routed = freight.Stops
            .OrderBy(s => s.Sequence)
            .Select(s => new RoutedStop(found[s.AddressId], s.Type))
            .ToList();
        var map = await _routes.PlanAsync(routed, cancellationToken);
        if (map is { DistanceMeters: { } meters, DurationSeconds: { } seconds })
        {
            freight.SetRoute(meters, seconds, PolylineCodec.Encode(map.Path));
        }

        await _freights.AddAsync(freight, cancellationToken);
        await AlertCompatibleDriversAsync(freight, found, now, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FreightMapper.ToResponse(freight, found, 0));
    }

    public async Task<Result<IReadOnlyList<FreightSimpleResponse>>> ListAsync(
        Guid customerId, FreightStatus? status, CancellationToken cancellationToken)
    {
        var responses = await _queries.ListByCustomerAsync(customerId, status, cancellationToken);
        return Result.Success(responses);
    }

    public async Task<Result<FreightResponse>> GetAsync(Guid customerId, Guid freightId, CancellationToken cancellationToken)
    {
        var response = await _queries.GetForCustomerAsync(customerId, freightId, cancellationToken);
        return response is null
            ? Result.Failure<FreightResponse>(MarketplaceErrors.FreightNotFound)
            : Result.Success(response);
    }

    public async Task<Result> CancelAsync(Guid customerId, Guid freightId, CancellationToken cancellationToken)
    {
        var freight = await _freights.GetByIdAsync(freightId, cancellationToken);
        if (freight is null || freight.CustomerId != customerId)
        {
            return Result.Failure(MarketplaceErrors.FreightNotFound);
        }

        if (freight.Status != FreightStatus.Open)
        {
            return Result.Failure(MarketplaceErrors.FreightNotOpen);
        }

        freight.Cancel();
        foreach (var proposal in await _proposals.ListPendingByFreightAsync(freight.Id, cancellationToken))
        {
            proposal.Reject();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Stages one "new compatible freight" notification per matching driver, saved together with the freight: either
    /// both exist or neither does. The push itself goes out later through the dispatcher.
    /// </summary>
    private async Task AlertCompatibleDriversAsync(
        FreightRequest freight, IReadOnlyDictionary<Guid, Address> addresses, DateTime now, CancellationToken cancellationToken)
    {
        var startsAt = freight.ScheduledPickupAt ?? now;
        var criteria = new FreightMatchCriteria(
            freight.CustomerId, freight.RequiredVehicleType, freight.CargoWeightKg, startsAt,
            startsAt + DeliveryPlanning.AgendaWindow(freight.Stops.Count));

        var userIds = await _matching.ListUserIdsForNewFreightAsync(criteria, MaxAlertedDrivers, cancellationToken);
        if (userIds.Count == 0)
        {
            return;
        }

        var ordered = freight.Stops.OrderBy(s => s.Sequence).ToList();
        var origin = addresses[ordered[0].AddressId].City;
        var destination = addresses[ordered[^1].AddressId].City;

        await _notifications.PublishAsync(
            userIds.Select(id => NotificationMessages.NewFreightAvailable(id, freight.Id, origin, destination, freight.IsSameDay)).ToList(),
            cancellationToken);
    }
}
