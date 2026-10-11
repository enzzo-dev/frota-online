using FluentValidation;
using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Deliveries.Dtos;
using FrotaOnline.Application.Drivers;
using FrotaOnline.Application.Marketplace;
using FrotaOnline.Application.Notifications;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Deliveries;

public sealed class DeliveryProgressService : IDeliveryProgressService
{
    private readonly IDriverRepository _drivers;
    private readonly IDeliveryRepository _deliveries;
    private readonly IFreightRepository _freights;
    private readonly IDriverAgendaRepository _agenda;
    private readonly IMediaFileRepository _media;
    private readonly IFeeLedgerRepository _fees;
    private readonly INotificationPublisher _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<CompleteStopRequest> _completeValidator;
    private readonly IValidator<ReportIncidentRequest> _incidentValidator;
    private readonly IValidator<CancelDeliveryRequest> _cancelValidator;

    public DeliveryProgressService(
        IDriverRepository drivers,
        IDeliveryRepository deliveries,
        IFreightRepository freights,
        IDriverAgendaRepository agenda,
        IMediaFileRepository media,
        IFeeLedgerRepository fees,
        INotificationPublisher notifications,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IValidator<CompleteStopRequest> completeValidator,
        IValidator<ReportIncidentRequest> incidentValidator,
        IValidator<CancelDeliveryRequest> cancelValidator)
    {
        _drivers = drivers;
        _deliveries = deliveries;
        _freights = freights;
        _agenda = agenda;
        _media = media;
        _fees = fees;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _completeValidator = completeValidator;
        _incidentValidator = incidentValidator;
        _cancelValidator = cancelValidator;
    }

    public async Task<Result<DeliveryProgressResponse>> StartAsync(
        Guid userId, Guid deliveryId, CancellationToken cancellationToken)
    {
        var loaded = await LoadForDriverAsync(userId, deliveryId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<DeliveryProgressResponse>(loaded.Error!);
        }

        var (delivery, freight) = loaded.Value;
        if (delivery.Status != DeliveryStatus.Scheduled)
        {
            return Result.Failure<DeliveryProgressResponse>(
                DeliveryErrors.InvalidStatus($"The delivery is {delivery.Status}; only a scheduled delivery can be started."));
        }

        var now = _clock.UtcNow;
        delivery.Advance(DeliveryStatus.ToPickup, now);
        await _deliveries.AddEventAsync(NewEvent(delivery, DeliveryStatus.ToPickup, now), cancellationToken);
        await _notifications.PublishAsync(
            NotificationMessages.DeliveryStarted(delivery.CustomerId, delivery.Id), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(Progress(delivery, freight));
    }

    public async Task<Result<DeliveryProgressResponse>> CompleteStopAsync(
        Guid userId, Guid deliveryId, Guid stopId, CompleteStopRequest request, CancellationToken cancellationToken)
    {
        var validation = await _completeValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<DeliveryProgressResponse>(ValidationMapper.ToError(validation));
        }

        var loaded = await LoadForDriverAsync(userId, deliveryId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<DeliveryProgressResponse>(loaded.Error!);
        }

        var (delivery, freight) = loaded.Value;
        if (delivery.Status is not (DeliveryStatus.ToPickup or DeliveryStatus.PickedUp or DeliveryStatus.InTransit))
        {
            return Result.Failure<DeliveryProgressResponse>(DeliveryErrors.InvalidStatus(
                $"The delivery is {delivery.Status}; start it before completing stops."));
        }

        var ordered = freight.Stops.OrderBy(s => s.Sequence).ToList();
        var stop = ordered.FirstOrDefault(s => s.Id == stopId);
        if (stop is null)
        {
            return Result.Failure<DeliveryProgressResponse>(DeliveryErrors.StopNotFound);
        }

        var next = ordered.First(s => !s.IsCompleted);
        if (next.Id != stop.Id)
        {
            return Result.Failure<DeliveryProgressResponse>(DeliveryErrors.StopOutOfOrder(next.Sequence));
        }

        if (request.ProofMediaFileId is { } proofId)
        {
            var proof = await _media.GetByIdAsync(proofId, cancellationToken);
            if (proof is null || proof.OwnerUserId != userId || proof.Kind != MediaKind.DeliveryProof)
            {
                return Result.Failure<DeliveryProgressResponse>(DeliveryErrors.InvalidProof);
            }
        }

        var now = _clock.UtcNow;
        var firstPickup = delivery.Status == DeliveryStatus.ToPickup;
        stop.Complete(now);

        // Collecting the first cargo puts the truck on the road; later stops keep it in transit.
        if (delivery.Status == DeliveryStatus.ToPickup)
        {
            delivery.Advance(DeliveryStatus.PickedUp, now);
            await _deliveries.AddEventAsync(NewEvent(delivery, DeliveryStatus.PickedUp, now), cancellationToken);
        }

        var finished = ordered.All(s => s.IsCompleted);
        if (!finished)
        {
            if (delivery.Status == DeliveryStatus.PickedUp)
            {
                delivery.Advance(DeliveryStatus.InTransit, now);
                await _deliveries.AddEventAsync(
                    DetailedEvent(delivery, DeliveryStatus.InTransit, now, request), cancellationToken);
            }
        }
        else
        {
            if (delivery.Status == DeliveryStatus.PickedUp)
            {
                delivery.Advance(DeliveryStatus.InTransit, now);
                await _deliveries.AddEventAsync(NewEvent(delivery, DeliveryStatus.InTransit, now), cancellationToken);
            }

            delivery.Advance(DeliveryStatus.Delivered, now);
            await _deliveries.AddEventAsync(
                DetailedEvent(delivery, DeliveryStatus.Delivered, now, request), cancellationToken);

            await _fees.AddAsync(FeeLedger.Accrue(delivery.DriverProfileId, delivery.Id, delivery.PlatformFee), cancellationToken);
            await ReleaseAgendaAsync(delivery.Id, now, cancellationToken);
        }

        // One notification per action: the most meaningful thing that just happened.
        var completed = ordered.Count(s => s.IsCompleted);
        var message = finished
            ? NotificationMessages.DeliveryCompleted(delivery.CustomerId, delivery.Id)
            : firstPickup
                ? NotificationMessages.DeliveryPickedUp(delivery.CustomerId, delivery.Id)
                : NotificationMessages.DeliveryStopCompleted(delivery.CustomerId, delivery.Id, completed, ordered.Count);
        await _notifications.PublishAsync(message, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Progress(delivery, freight));
    }

    public async Task<Result<DeliveryProgressResponse>> ReportIncidentAsync(
        Guid userId, Guid deliveryId, ReportIncidentRequest request, CancellationToken cancellationToken)
    {
        var validation = await _incidentValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<DeliveryProgressResponse>(ValidationMapper.ToError(validation));
        }

        var loaded = await LoadForDriverAsync(userId, deliveryId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<DeliveryProgressResponse>(loaded.Error!);
        }

        var (delivery, freight) = loaded.Value;
        if (!delivery.CanTransitionTo(DeliveryStatus.Incident))
        {
            return Result.Failure<DeliveryProgressResponse>(DeliveryErrors.InvalidStatus(
                $"An incident cannot be reported while the delivery is {delivery.Status}."));
        }

        var now = _clock.UtcNow;
        delivery.Advance(DeliveryStatus.Incident, now);
        await _deliveries.AddEventAsync(
            DeliveryEvent.Create(delivery.Id, DeliveryStatus.Incident, now, request.Note, null, null, null),
            cancellationToken);
        await _notifications.PublishAsync(
            NotificationMessages.DeliveryIncident(delivery.CustomerId, delivery.Id, request.Note), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(Progress(delivery, freight));
    }

    public async Task<Result<DeliveryProgressResponse>> CancelAsDriverAsync(
        Guid userId, Guid deliveryId, CancelDeliveryRequest request, CancellationToken cancellationToken)
    {
        var validation = await _cancelValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<DeliveryProgressResponse>(ValidationMapper.ToError(validation));
        }

        var loaded = await LoadForDriverAsync(userId, deliveryId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<DeliveryProgressResponse>(loaded.Error!);
        }

        var (delivery, freight) = loaded.Value;
        return await CancelAsync(delivery, freight, request.Reason, "driver", cancellationToken);
    }

    public async Task<Result<DeliveryProgressResponse>> CancelAsCustomerAsync(
        Guid customerId, Guid deliveryId, CancelDeliveryRequest request, CancellationToken cancellationToken)
    {
        var validation = await _cancelValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<DeliveryProgressResponse>(ValidationMapper.ToError(validation));
        }

        var delivery = await _deliveries.GetByIdAsync(deliveryId, cancellationToken);
        if (delivery is null || delivery.CustomerId != customerId)
        {
            return Result.Failure<DeliveryProgressResponse>(MarketplaceErrors.DeliveryNotFound);
        }

        if (delivery.Status is not (DeliveryStatus.Scheduled or DeliveryStatus.ToPickup))
        {
            return Result.Failure<DeliveryProgressResponse>(DeliveryErrors.InvalidStatus(
                $"The delivery is {delivery.Status}; you can only cancel before the cargo is picked up."));
        }

        var freight = await _freights.GetByIdAsync(delivery.FreightRequestId, cancellationToken);
        if (freight is null)
        {
            return Result.Failure<DeliveryProgressResponse>(MarketplaceErrors.FreightNotFound);
        }

        return await CancelAsync(delivery, freight, request.Reason, "customer", cancellationToken);
    }

    private async Task<Result<DeliveryProgressResponse>> CancelAsync(
        Delivery delivery, FreightRequest freight, string reason, string who, CancellationToken cancellationToken)
    {
        if (!delivery.CanTransitionTo(DeliveryStatus.Cancelled))
        {
            return Result.Failure<DeliveryProgressResponse>(DeliveryErrors.InvalidStatus(
                $"A delivery in {delivery.Status} cannot be cancelled."));
        }

        var now = _clock.UtcNow;
        delivery.Advance(DeliveryStatus.Cancelled, now);
        await _deliveries.AddEventAsync(
            DeliveryEvent.Create(delivery.Id, DeliveryStatus.Cancelled, now, $"Cancelled by the {who}: {reason}", null, null, null),
            cancellationToken);
        await ReleaseAgendaAsync(delivery.Id, now, cancellationToken);

        // The other side is told: the customer when the driver cancels, the driver when the customer does.
        if (who == "driver")
        {
            await _notifications.PublishAsync(
                NotificationMessages.DeliveryCancelledByDriver(delivery.CustomerId, delivery.Id, reason), cancellationToken);
        }
        else
        {
            var driver = await _drivers.GetByIdAsync(delivery.DriverProfileId, cancellationToken);
            if (driver is not null)
            {
                await _notifications.PublishAsync(
                    NotificationMessages.DeliveryCancelledByCustomer(driver.UserId, delivery.Id, reason), cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(Progress(delivery, freight));
    }

    private async Task<Result<(Delivery Delivery, FreightRequest Freight)>> LoadForDriverAsync(
        Guid userId, Guid deliveryId, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<(Delivery, FreightRequest)>(DriverErrors.ProfileNotFound);
        }

        var delivery = await _deliveries.GetByIdAsync(deliveryId, cancellationToken);
        if (delivery is null || delivery.DriverProfileId != profile.Id)
        {
            return Result.Failure<(Delivery, FreightRequest)>(MarketplaceErrors.DeliveryNotFound);
        }

        var freight = await _freights.GetByIdAsync(delivery.FreightRequestId, cancellationToken);
        return freight is null
            ? Result.Failure<(Delivery, FreightRequest)>(MarketplaceErrors.FreightNotFound)
            : Result.Success((delivery, freight));
    }

    private async Task ReleaseAgendaAsync(Guid deliveryId, DateTime now, CancellationToken cancellationToken)
    {
        var slot = await _agenda.GetByDeliveryIdAsync(deliveryId, cancellationToken);
        slot?.Release(now);
    }

    private static DeliveryEvent NewEvent(Delivery delivery, DeliveryStatus status, DateTime now) =>
        DeliveryEvent.Create(delivery.Id, status, now, null, null, null, null);

    private static DeliveryEvent DetailedEvent(Delivery delivery, DeliveryStatus status, DateTime now, CompleteStopRequest request) =>
        DeliveryEvent.Create(delivery.Id, status, now, request.Note, request.Latitude, request.Longitude, request.ProofMediaFileId);

    private static DeliveryProgressResponse Progress(Delivery delivery, FreightRequest freight)
    {
        var ordered = freight.Stops.OrderBy(s => s.Sequence).ToList();
        var next = ordered.FirstOrDefault(s => !s.IsCompleted);
        return new DeliveryProgressResponse(
            delivery.Id, delivery.Status, ordered.Count, ordered.Count(s => s.IsCompleted), next?.Id);
    }
}
