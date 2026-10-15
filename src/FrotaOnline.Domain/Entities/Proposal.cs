using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Domain.Entities;

public sealed class Proposal : BaseEntity, IAggregateRoot
{
    private Proposal()
    {
    }

    public Guid FreightRequestId { get; private set; }

    public Guid DriverProfileId { get; private set; }

    public Guid VehicleId { get; private set; }

    /// <summary>Price set by the driver (BRL).</summary>
    public decimal Price { get; private set; }

    public string? Message { get; private set; }

    public ProposalStatus Status { get; private set; } = ProposalStatus.Pending;

    public DateTime ExpiresAt { get; private set; }

    public static Proposal Create(
        Guid freightRequestId, Guid driverProfileId, Guid vehicleId, decimal price, string? message, DateTime expiresAt) => new()
    {
        FreightRequestId = Guard.NotEmpty(freightRequestId, nameof(freightRequestId)),
        DriverProfileId = Guard.NotEmpty(driverProfileId, nameof(driverProfileId)),
        VehicleId = Guard.NotEmpty(vehicleId, nameof(vehicleId)),
        Price = Guard.Positive(price, nameof(price)),
        Message = message?.Trim(),
        ExpiresAt = expiresAt
    };

    public void Accept() => Transition(ProposalStatus.Accepted);

    public void Reject() => Transition(ProposalStatus.Rejected);

    public void Withdraw() => Transition(ProposalStatus.Withdrawn);

    public bool ExpireIfDue(DateTime utcNow)
    {
        if (Status != ProposalStatus.Pending || utcNow < ExpiresAt)
        {
            return false;
        }

        Status = ProposalStatus.Expired;
        return true;
    }

    private void Transition(ProposalStatus next)
    {
        if (Status != ProposalStatus.Pending)
        {
            throw new DomainException($"Uma proposta com status {Status} não pode passar para {next}.");
        }

        Status = next;
    }
}
