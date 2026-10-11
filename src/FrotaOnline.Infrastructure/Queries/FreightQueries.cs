using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Addresses.Dtos;
using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Queries;

/// <summary>
/// Every query here is an <see cref="IQueryable{T}"/> translated to SQL with AsNoTracking and projected
/// straight into columns: no aggregate is loaded, nothing is filtered, sorted, counted or joined in memory.
/// A list costs a fixed number of round trips (headers, stops, items), however many freights it has.
/// </summary>
internal sealed class FreightQueries : IFreightQueries
{
    private const int OpenListLimit = 100;

    private readonly AppDbContext _db;

    public FreightQueries(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<OpenFreightResponse>> ListOpenAsync(
        DateTime utcNow, IReadOnlyCollection<VehicleType> vehicleTypes, CancellationToken cancellationToken)
    {
        var headers = await _db.FreightRequests
            .AsNoTracking()
            .Where(f => f.Status == FreightStatus.Open
                        && f.ProposalDeadline > utcNow
                        && vehicleTypes.Contains(f.RequiredVehicleType))
            .OrderByDescending(f => f.CreatedAt)
            .ThenBy(f => f.Id)
            .Take(OpenListLimit)
            .Select(f => new
            {
                f.Id,
                f.CargoDescription,
                f.CargoWeightKg,
                f.RequiredVehicleType,
                f.ScheduledPickupAt,
                f.ProposalDeadline,
                f.RouteDistanceMeters,
                f.RouteDurationSeconds,
            })
            .ToListAsync(cancellationToken);

        if (headers.Count == 0)
        {
            return Array.Empty<OpenFreightResponse>();
        }

        var freightIds = headers.Select(h => h.Id).ToList();

        // Before approval the driver only sees the area, never the street: the street is not even selected.
        // The coordinates are read only to be rounded to about 1 km below; the exact ones never leave this method.
        var stops = await (
                from s in _db.FreightStops.AsNoTracking()
                where freightIds.Contains(s.FreightRequestId)
                join a in _db.Addresses.AsNoTracking() on s.AddressId equals a.Id
                orderby s.FreightRequestId, s.Sequence
                select new { s.FreightRequestId, StopId = s.Id, s.Sequence, s.Type, a.District, a.City, a.State, a.Latitude, a.Longitude })
            .ToListAsync(cancellationToken);

        var items = await LoadItemsAsync(freightIds, cancellationToken);
        var stopsByFreight = stops.ToLookup(s => s.FreightRequestId);

        return headers
            .Select(h =>
            {
                var rows = stopsByFreight[h.Id]
                    .Select(s => new OpenStopResponse(
                        s.Sequence, s.Type, s.District, s.City, s.State,
                        ApproximateCoordinate(s.Latitude), ApproximateCoordinate(s.Longitude), ItemsOf(items, s.StopId)))
                    .ToList();

                return new OpenFreightResponse(
                    h.Id, h.CargoDescription, h.CargoWeightKg, h.RequiredVehicleType, h.ScheduledPickupAt, h.ProposalDeadline,
                    rows.Count(r => r.Type == StopType.Pickup), rows.Count(r => r.Type == StopType.Delivery),
                    h.RouteDistanceMeters, h.RouteDurationSeconds, rows);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<FreightResponse>> ListByCustomerAsync(
        Guid customerId, FreightStatus? status, CancellationToken cancellationToken)
    {
        var query = _db.FreightRequests.AsNoTracking().Where(f => f.CustomerId == customerId);
        if (status is not null)
        {
            query = query.Where(f => f.Status == status);
        }

        return await ToResponsesAsync(query.OrderByDescending(f => f.CreatedAt).ThenBy(f => f.Id), cancellationToken);
    }

    public async Task<FreightResponse?> GetForCustomerAsync(
        Guid customerId, Guid freightId, CancellationToken cancellationToken)
    {
        var rows = await ToResponsesAsync(
            _db.FreightRequests.AsNoTracking().Where(f => f.Id == freightId && f.CustomerId == customerId),
            cancellationToken);

        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<IReadOnlyList<RankedProposalResponse>?> ListRankedProposalsAsync(
        Guid customerId, Guid freightId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var owned = await _db.FreightRequests
            .AsNoTracking()
            .AnyAsync(f => f.Id == freightId && f.CustomerId == customerId, cancellationToken);
        if (!owned)
        {
            return null;
        }

        // One statement: filter, joins and ordering all run in PostgreSQL (price, then who proposed first).
        var rows = await (
                from p in _db.Proposals.AsNoTracking()
                where p.FreightRequestId == freightId
                      && p.Status == ProposalStatus.Pending
                      && p.ExpiresAt > utcNow
                join d in _db.DriverProfiles.AsNoTracking() on p.DriverProfileId equals d.Id
                join u in _db.Users.AsNoTracking() on d.UserId equals u.Id
                join v in _db.Vehicles.AsNoTracking() on p.VehicleId equals v.Id
                orderby p.Price, p.CreatedAt, p.Id
                select new
                {
                    ProposalId = p.Id,
                    p.Price,
                    p.Message,
                    DriverName = u.FullName,
                    VehicleType = v.Type,
                    VehicleModel = v.Model,
                    p.ExpiresAt,
                    p.CreatedAt,
                })
            .ToListAsync(cancellationToken);

        return rows
            .Select((r, index) => new RankedProposalResponse(
                index + 1, r.ProposalId, r.Price, r.Message, r.DriverName, r.VehicleType, r.VehicleModel, r.ExpiresAt, r.CreatedAt))
            .ToList();
    }

    private async Task<IReadOnlyList<FreightResponse>> ToResponsesAsync(
        IQueryable<FreightRequest> ordered, CancellationToken cancellationToken)
    {
        // The pending-proposal counter is a correlated subquery, so there is no second pass over proposals.
        var headers = await ordered
            .Select(f => new
            {
                f.Id,
                f.Status,
                f.CargoDescription,
                f.CargoWeightKg,
                f.RequiredVehicleType,
                f.ScheduledPickupAt,
                f.ProposalDeadline,
                f.ApprovedProposalId,
                f.RouteDistanceMeters,
                f.RouteDurationSeconds,
                f.CreatedAt,
                PendingProposals = _db.Proposals.Count(
                    p => p.FreightRequestId == f.Id && p.Status == ProposalStatus.Pending),
            })
            .ToListAsync(cancellationToken);

        if (headers.Count == 0)
        {
            return Array.Empty<FreightResponse>();
        }

        var freightIds = headers.Select(h => h.Id).ToList();

        var stops = await (
                from s in _db.FreightStops.AsNoTracking()
                where freightIds.Contains(s.FreightRequestId)
                join a in _db.Addresses.AsNoTracking() on s.AddressId equals a.Id
                orderby s.FreightRequestId, s.Sequence
                select new
                {
                    s.FreightRequestId,
                    StopId = s.Id,
                    s.Sequence,
                    s.Type,
                    s.Note,
                    s.CompletedAt,
                    Address = new AddressResponse(
                        a.Id, a.Label, a.Street, a.Number, a.Complement, a.District, a.City, a.State, a.ZipCode,
                        a.Latitude, a.Longitude),
                })
            .ToListAsync(cancellationToken);

        var items = await LoadItemsAsync(freightIds, cancellationToken);
        var stopsByFreight = stops.ToLookup(s => s.FreightRequestId);

        return headers
            .Select(h =>
            {
                var rows = stopsByFreight[h.Id]
                    .Select(s => new FreightStopResponse(
                        s.StopId, s.Sequence, s.Type, s.Address, s.Note, ItemsOf(items, s.StopId), s.CompletedAt))
                    .ToList();

                return new FreightResponse(
                    h.Id, h.Status, h.CargoDescription, h.CargoWeightKg, h.RequiredVehicleType, h.ScheduledPickupAt,
                    h.ProposalDeadline, rows.Count(r => r.Type == StopType.Pickup), rows.Count(r => r.Type == StopType.Delivery),
                    h.RouteDistanceMeters, h.RouteDurationSeconds, rows, h.ApprovedProposalId, h.PendingProposals, h.CreatedAt);
            })
            .ToList();
    }

    private async Task<Dictionary<Guid, List<FreightItemResponse>>> LoadItemsAsync(
        List<Guid> freightIds, CancellationToken cancellationToken) =>
        await FreightItemReader.LoadAsync(_db, freightIds, cancellationToken);

    /// <summary>Two decimals of a degree is about 1.1 km: enough to place the pin in a neighbourhood, not on a street.</summary>
    private static double? ApproximateCoordinate(double? value) => value is null ? null : Math.Round(value.Value, 2);

    private static IReadOnlyList<FreightItemResponse> ItemsOf(Dictionary<Guid, List<FreightItemResponse>> items, Guid stopId) =>
        items.TryGetValue(stopId, out var list) ? list : Array.Empty<FreightItemResponse>();
}
