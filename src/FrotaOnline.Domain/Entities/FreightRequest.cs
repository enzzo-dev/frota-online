using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Domain.Entities;

public sealed class FreightRequest : BaseEntity, IAggregateRoot
{
    private readonly List<FreightStop> _stops = new();

    private FreightRequest()
    {
    }

    public Guid CustomerId { get; private set; }


    public string CargoDescription { get; private set; } = string.Empty;

    public string? DriverNote { get; private set; }

    /// <summary>Total weight of the whole freight, declared by the customer.</summary>
    public decimal CargoWeightKg { get; private set; }

    public VehicleType RequiredVehicleType { get; private set; }

    /// <summary>Null means "same day / as soon as possible".</summary>
    public DateTime? ScheduledPickupAt { get; private set; }

    public DateTime ProposalDeadline { get; private set; }

    public FreightStatus Status { get; private set; } = FreightStatus.Open;

    public Guid? ApprovedProposalId { get; private set; }

    /// <summary>Driving distance of the whole run, calculated when the freight was created. Null when it could not be calculated.</summary>
    public int? RouteDistanceMeters { get; private set; }

    public int? RouteDurationSeconds { get; private set; }

    public string? RoutePolyline { get; private set; }

    public bool IsSameDay => ScheduledPickupAt is null;

    /// <summary>Pickups and deliveries in the order the customer chose. The last one is the final delivery.</summary>
    public IReadOnlyList<FreightStop> Stops => _stops;

    public int PickupCount => _stops.Count(s => s.Type == StopType.Pickup);

    public int DeliveryCount => _stops.Count(s => s.Type == StopType.Delivery);

    public static FreightRequest Create(
        Guid customerId, IReadOnlyList<FreightStopDraft> stops, string cargoDescription,
        decimal cargoWeightKg, VehicleType requiredVehicleType, DateTime? scheduledPickupAt, DateTime utcNow,
        string? driverNote = null)
    {
        if (scheduledPickupAt is not null && scheduledPickupAt <= utcNow)
        {
            throw new DomainException("A coleta agendada deve ser no futuro.");
        }

        if (stops is null || stops.Count < 2)
        {
            throw new DomainException("O frete precisa de ao menos uma coleta e uma entrega.");
        }

        if (stops.Count > FreightLimits.MaxStops)
        {
            throw new DomainException($"O frete pode ter no máximo {FreightLimits.MaxStops} paradas.");
        }

        if (stops[0].Type != StopType.Pickup)
        {
            throw new DomainException("A primeira parada deve ser uma coleta.");
        }

        if (stops[^1].Type != StopType.Delivery)
        {
            throw new DomainException("A última parada deve ser uma entrega: é o endereço final de entrega.");
        }

        var addressIds = stops.Select(s => s.AddressId).ToList();
        if (addressIds.Distinct().Count() != addressIds.Count)
        {
            throw new DomainException("Cada parada deve usar um endereço diferente.");
        }

        var note = string.IsNullOrWhiteSpace(driverNote) ? null : driverNote.Trim();
        if (note is not null && note.Length > FreightLimits.MaxDriverNoteLength)
        {
            throw new DomainException($"A observação para o motorista pode ter no máximo {FreightLimits.MaxDriverNoteLength} caracteres.");
        }

        var deadline = scheduledPickupAt is null
            ? utcNow + ProposalWindows.SameDay
            : Min(utcNow + ProposalWindows.Scheduled, scheduledPickupAt.Value);

        var freight = new FreightRequest
        {
            CustomerId = Guard.NotEmpty(customerId, nameof(customerId)),
            CargoDescription = Guard.NotNullOrWhiteSpace(cargoDescription, nameof(cargoDescription)),
            CargoWeightKg = Guard.Positive(cargoWeightKg, nameof(cargoWeightKg)),
            RequiredVehicleType = requiredVehicleType,
            DriverNote = note,
            ScheduledPickupAt = scheduledPickupAt,
            ProposalDeadline = deadline
        };

        for (var i = 0; i < stops.Count; i++)
        {
            freight._stops.Add(FreightStop.Create(freight.Id, i + 1, stops[i]));
        }

        return freight;
    }

    public void SetRoute(int distanceMeters, int durationSeconds, string? polyline = null)
    {
        if (distanceMeters < 0 || durationSeconds < 0)
        {
            throw new DomainException("A distância e a duração da rota não podem ser negativas.");
        }

        RouteDistanceMeters = distanceMeters;
        RouteDurationSeconds = durationSeconds;
        RoutePolyline = string.IsNullOrEmpty(polyline) ? null : polyline;
    }

    public bool CanReceiveProposals(DateTime utcNow) => Status == FreightStatus.Open && utcNow < ProposalDeadline;

    public void Approve(Guid proposalId, DateTime utcNow)
    {
        if (Status != FreightStatus.Open)
        {
            throw new DomainException($"Um frete com status {Status} não pode ser aprovado.");
        }

        Status = FreightStatus.Approved;
        ApprovedProposalId = Guard.NotEmpty(proposalId, nameof(proposalId));
        AddDomainEvent(new FreightApprovedEvent(Id, proposalId, utcNow));
    }

    public void Cancel()
    {
        if (Status != FreightStatus.Open)
        {
            throw new DomainException($"Um frete com status {Status} não pode ser cancelado.");
        }

        Status = FreightStatus.Cancelled;
    }

    /// <summary>Returns true when the freight was moved to Expired.</summary>
    public bool ExpireIfDue(DateTime utcNow)
    {
        if (Status != FreightStatus.Open || utcNow < ProposalDeadline)
        {
            return false;
        }

        Status = FreightStatus.Expired;
        return true;
    }

    private static DateTime Min(DateTime a, DateTime b) => a <= b ? a : b;
}

public sealed record FreightApprovedEvent(Guid FreightRequestId, Guid ProposalId, DateTime OccurredAt) : IDomainEvent;
