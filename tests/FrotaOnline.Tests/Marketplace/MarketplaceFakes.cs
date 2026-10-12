using FrotaOnline.Application.Abstractions.Addresses;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Abstractions.Routing;
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
        Task.FromResult<IReadOnlyList<Address>>(Items.Where(a => a.OwnerUserId == ownerUserId && !a.IsDeleted).ToList());

    public Task<IReadOnlyList<Address>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Address>>(Items.Where(a => ids.Contains(a.Id)).ToList());

    public Task AddAsync(Address address, CancellationToken cancellationToken)
    {
        Items.Add(address);
        return Task.CompletedTask;
    }

    public void Remove(Address address) => Items.Remove(address);
}

internal sealed class FakeFreightRepository : IFreightRepository
{
    public List<FreightRequest> Items { get; } = new();

    public Task<FreightRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(f => f.Id == id));

    public Task<IReadOnlyList<ExpiredFreight>> ExpireDueAsync(DateTime utcNow, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ExpiredFreight>>(
            Items.ToList().Where(f => f.ExpireIfDue(utcNow)).Select(f => new ExpiredFreight(f.Id, f.CustomerId)).ToList());

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

    public Task<IReadOnlyList<ExpiredProposal>> ExpireDueAsync(DateTime utcNow, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ExpiredProposal>>(
            Items.ToList().Where(p => p.ExpireIfDue(utcNow)).Select(p => new ExpiredProposal(p.Id, p.DriverProfileId)).ToList());

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

    public Task<Delivery?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(d => d.Id == id));

    public Task<int> CountTakenByDriverAsync(Guid driverProfileId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Count(d => d.DriverProfileId == driverProfileId && d.Status != DeliveryStatus.Cancelled));

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

    public Task<DriverAgendaSlot?> GetByDeliveryIdAsync(Guid deliveryId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(s => s.DeliveryId == deliveryId));

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

internal sealed class FakePaymentRepository : IPaymentRepository
{
    public List<Payment> Items { get; } = new();

    public Task<bool> ExistsForDeliveryAsync(Guid deliveryId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(p => p.DeliveryId == deliveryId));

    public Task<Payment?> GetByDeliveryIdAsync(Guid deliveryId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(p => p.DeliveryId == deliveryId));

    public Task<Payment?> GetByGatewayIdAsync(string gatewayPaymentId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(p => p.GatewayPaymentId == gatewayPaymentId));

    public Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        Items.Add(payment);
        return Task.CompletedTask;
    }
}

internal sealed class FakeFeeLedgerRepository : IFeeLedgerRepository
{
    public List<FeeLedger> Items { get; } = new();

    public Task<decimal> GetBalanceAsync(Guid driverProfileId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Where(e => e.DriverProfileId == driverProfileId)
            .Sum(e => e.EntryType == FeeEntryType.FeeAccrued ? e.Amount : -e.Amount));

    public Task AddAsync(FeeLedger entry, CancellationToken cancellationToken)
    {
        Items.Add(entry);
        return Task.CompletedTask;
    }
}

/// <summary>Straight-line stand-in for the routing engine. Set <see cref="Available"/> to false to simulate an outage.</summary>
internal sealed class FakeRoutingProvider : IRoutingProvider
{
    public bool Available { get; set; } = true;

    public int Calls { get; private set; }

    public List<GeoPoint> LastPoints { get; private set; } = new();

    public Task<RoutePath?> GetRouteAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken)
    {
        Calls++;
        LastPoints = points.ToList();
        return Task.FromResult<RoutePath?>(Available ? new RoutePath(12_500, 1_800, points) : null);
    }
}

/// <summary>Same rule as the SQL: open freight or unfinished delivery = Active; any other freight = History.</summary>
internal sealed class FakeAddressUsageQueries : IAddressUsageQueries
{
    private readonly FakeFreightRepository _freights;
    private readonly FakeDeliveryRepository _deliveries;

    public FakeAddressUsageQueries(FakeFreightRepository freights, FakeDeliveryRepository deliveries)
    {
        _freights = freights;
        _deliveries = deliveries;
    }

    public Task<AddressUsage> GetUsageAsync(Guid addressId, CancellationToken cancellationToken)
    {
        var freights = _freights.Items.Where(f => f.Stops.Any(s => s.AddressId == addressId)).ToList();
        var active = freights.Any(f =>
            f.Status == FreightStatus.Open
            || (f.Status == FreightStatus.Approved && _deliveries.Items.Any(d =>
                d.FreightRequestId == f.Id && d.Status != DeliveryStatus.Delivered && d.Status != DeliveryStatus.Cancelled)));

        return Task.FromResult(active ? AddressUsage.Active : freights.Count > 0 ? AddressUsage.History : AddressUsage.None);
    }
}
