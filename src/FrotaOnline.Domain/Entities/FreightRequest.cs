using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Domain.Entities;

public sealed class FreightRequest : BaseEntity, IAggregateRoot
{
    private FreightRequest()
    {
    }

    public Guid CustomerId { get; private set; }

    public Guid PickupAddressId { get; private set; }

    public Guid DeliveryAddressId { get; private set; }

    public string CargoDescription { get; private set; } = string.Empty;

    public decimal CargoWeightKg { get; private set; }

    public VehicleType RequiredVehicleType { get; private set; }

    /// <summary>Null means "same day / as soon as possible".</summary>
    public DateTime? ScheduledPickupAt { get; private set; }

    public DateTime ProposalDeadline { get; private set; }

    public FreightStatus Status { get; private set; } = FreightStatus.Open;

    public Guid? ApprovedProposalId { get; private set; }

    public bool IsSameDay => ScheduledPickupAt is null;

    public static FreightRequest Create(
        Guid customerId, Guid pickupAddressId, Guid deliveryAddressId, string cargoDescription,
        decimal cargoWeightKg, VehicleType requiredVehicleType, DateTime? scheduledPickupAt, DateTime utcNow)
    {
        if (scheduledPickupAt is not null && scheduledPickupAt <= utcNow)
        {
            throw new DomainException("The scheduled pickup must be in the future.");
        }

        var deadline = scheduledPickupAt is null
            ? utcNow + ProposalWindows.SameDay
            : Min(utcNow + ProposalWindows.Scheduled, scheduledPickupAt.Value);

        return new FreightRequest
        {
            CustomerId = Guard.NotEmpty(customerId, nameof(customerId)),
            PickupAddressId = Guard.NotEmpty(pickupAddressId, nameof(pickupAddressId)),
            DeliveryAddressId = Guard.NotEmpty(deliveryAddressId, nameof(deliveryAddressId)),
            CargoDescription = Guard.NotNullOrWhiteSpace(cargoDescription, nameof(cargoDescription)),
            CargoWeightKg = Guard.Positive(cargoWeightKg, nameof(cargoWeightKg)),
            RequiredVehicleType = requiredVehicleType,
            ScheduledPickupAt = scheduledPickupAt,
            ProposalDeadline = deadline
        };
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
