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

    /// <summary>Total weight of the whole freight, declared by the customer.</summary>
    public decimal CargoWeightKg { get; private set; }

    public VehicleType RequiredVehicleType { get; private set; }

    /// <summary>Null means "same day / as soon as possible".</summary>
    public DateTime? ScheduledPickupAt { get; private set; }

    public DateTime ProposalDeadline { get; private set; }

    public FreightStatus Status { get; private set; } = FreightStatus.Open;

    public Guid? ApprovedProposalId { get; private set; }

    public bool IsSameDay => ScheduledPickupAt is null;

    /// <summary>Pickups and deliveries in the order the customer chose. The last one is the final delivery.</summary>
    public IReadOnlyList<FreightStop> Stops => _stops;

    public int PickupCount => _stops.Count(s => s.Type == StopType.Pickup);

    public int DeliveryCount => _stops.Count(s => s.Type == StopType.Delivery);

    public static FreightRequest Create(
        Guid customerId, IReadOnlyList<FreightStopDraft> stops, string cargoDescription,
        decimal cargoWeightKg, VehicleType requiredVehicleType, DateTime? scheduledPickupAt, DateTime utcNow)
    {
        if (scheduledPickupAt is not null && scheduledPickupAt <= utcNow)
        {
            throw new DomainException("The scheduled pickup must be in the future.");
        }

        if (stops is null || stops.Count < 2)
        {
            throw new DomainException("A freight needs at least one pickup and one delivery.");
        }

        if (stops.Count > FreightLimits.MaxStops)
        {
            throw new DomainException($"A freight can have at most {FreightLimits.MaxStops} stops.");
        }

        if (stops[0].Type != StopType.Pickup)
        {
            throw new DomainException("The first stop must be a pickup.");
        }

        if (stops[^1].Type != StopType.Delivery)
        {
            throw new DomainException("The last stop must be a delivery: it is the final delivery address.");
        }

        var addressIds = stops.Select(s => s.AddressId).ToList();
        if (addressIds.Distinct().Count() != addressIds.Count)
        {
            throw new DomainException("Every stop must use a different address.");
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
            ScheduledPickupAt = scheduledPickupAt,
            ProposalDeadline = deadline
        };

        for (var i = 0; i < stops.Count; i++)
        {
            freight._stops.Add(FreightStop.Create(freight.Id, i + 1, stops[i]));
        }

        return freight;
    }

    public bool CanReceiveProposals(DateTime utcNow) => Status == FreightStatus.Open && utcNow < ProposalDeadline;

    public void Approve(Guid proposalId, DateTime utcNow)
    {
        if (Status != FreightStatus.Open)
        {
            throw new DomainException($"A freight in status {Status} cannot be approved.");
        }

        Status = FreightStatus.Approved;
        ApprovedProposalId = Guard.NotEmpty(proposalId, nameof(proposalId));
        AddDomainEvent(new FreightApprovedEvent(Id, proposalId, utcNow));
    }

    public void Cancel()
    {
        if (Status != FreightStatus.Open)
        {
            throw new DomainException($"A freight in status {Status} cannot be cancelled.");
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
