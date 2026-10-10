using FrotaOnline.Application.Abstractions.Addresses;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Terms;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Tests.Marketplace;

internal sealed class FakeAddressRepository : IAddressRepository
{
    public List<Address> Items { get; } = new();

    public Task<Address?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(a => a.Id == id));

    public Task<IReadOnlyList<Address>> ListByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Address>>(Items.Where(a => a.OwnerUserId == ownerUserId).ToList());

    public Task<IReadOnlyList<Address>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Address>>(Items.Where(a => ids.Contains(a.Id)).ToList());

    public Task AddAsync(Address address, CancellationToken cancellationToken)
    {
        Items.Add(address);
        return Task.CompletedTask;
    }
}

internal sealed class FakeFreightRepository : IFreightRepository
{
    public List<FreightRequest> Items { get; } = new();

    public Task<FreightRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(f => f.Id == id));

    public Task<IReadOnlyList<FreightRequest>> ListByCustomerAsync(Guid customerId, FreightStatus? status, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<FreightRequest>>(
            Items.Where(f => f.CustomerId == customerId && (status is null || f.Status == status)).ToList());

    public Task<IReadOnlyList<FreightRequest>> ListOpenAsync(
        DateTime utcNow, IReadOnlyCollection<VehicleType> vehicleTypes, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<FreightRequest>>(
            Items.Where(f => f.Status == FreightStatus.Open && f.ProposalDeadline > utcNow && vehicleTypes.Contains(f.RequiredVehicleType)).ToList());

    public Task<IReadOnlyList<FreightRequest>> ListOpenDueAsync(DateTime utcNow, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<FreightRequest>>(
            Items.Where(f => f.Status == FreightStatus.Open && f.ProposalDeadline <= utcNow).ToList());

    public Task AddAsync(FreightRequest freight, CancellationToken cancellationToken)
    {
        Items.Add(freight);
        return Task.CompletedTask;
    }
}

internal sealed class FakeProposalRepository : IProposalRepository
{
    public List<Proposal> Items { get; } = new();

    public Task<Proposal?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(p => p.Id == id));

    public Task<IReadOnlyList<Proposal>> ListPendingByFreightAsync(Guid freightRequestId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Proposal>>(
            Items.Where(p => p.FreightRequestId == freightRequestId && p.Status == ProposalStatus.Pending).ToList());

    public Task<IReadOnlyList<Proposal>> ListByDriverAsync(Guid driverProfileId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Proposal>>(Items.Where(p => p.DriverProfileId == driverProfileId).ToList());

    public Task<IReadOnlyList<Proposal>> ListPendingDueAsync(DateTime utcNow, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Proposal>>(
            Items.Where(p => p.Status == ProposalStatus.Pending && p.ExpiresAt <= utcNow).ToList());

    public Task<IReadOnlyDictionary<Guid, int>> CountPendingByFreightAsync(
        IReadOnlyCollection<Guid> freightRequestIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(
            Items.Where(p => p.Status == ProposalStatus.Pending && freightRequestIds.Contains(p.FreightRequestId))
                .GroupBy(p => p.FreightRequestId)
                .ToDictionary(g => g.Key, g => g.Count()));

    public Task<bool> HasPendingAsync(Guid freightRequestId, Guid driverProfileId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(p =>
            p.FreightRequestId == freightRequestId && p.DriverProfileId == driverProfileId && p.Status == ProposalStatus.Pending));

    public Task AddAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        Items.Add(proposal);
        return Task.CompletedTask;
    }
}

internal sealed class FakeDeliveryRepository : IDeliveryRepository
{
    public List<Delivery> Items { get; } = new();

    public List<DeliveryEvent> Events { get; } = new();

    public Task AddAsync(Delivery delivery, CancellationToken cancellationToken)
    {
        Items.Add(delivery);
        return Task.CompletedTask;
    }

    public Task AddEventAsync(DeliveryEvent deliveryEvent, CancellationToken cancellationToken)
    {
        Events.Add(deliveryEvent);
        return Task.CompletedTask;
    }
}

internal sealed class FakeDriverAgendaRepository : IDriverAgendaRepository
{
    public List<DriverAgendaSlot> Items { get; } = new();

    public Task<bool> HasConflictAsync(Guid driverProfileId, DateTime startsAt, DateTime endsAt, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(s => s.DriverProfileId == driverProfileId && s.Overlaps(startsAt, endsAt)));

    public Task AddAsync(DriverAgendaSlot slot, CancellationToken cancellationToken)
    {
        Items.Add(slot);
        return Task.CompletedTask;
    }
}

internal sealed class FakeTermAcceptanceChecker : ITermAcceptanceChecker
{
    public bool Accepted { get; set; } = true;

    public Task<bool> HasAcceptedAllActiveAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(Accepted);
}
