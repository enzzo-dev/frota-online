using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Addresses.Dtos;
using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Queries;

internal sealed class DeliveryQueries : IDeliveryQueries
{
    private readonly AppDbContext _db;

    public DeliveryQueries(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<DriverDeliverySummary>> ListByDriverAsync(
        Guid driverProfileId, CancellationToken cancellationToken)
    {
        // Stop counters are correlated COUNT subqueries: no freight or stop is loaded.
        var rows = await (
                from d in _db.Deliveries.AsNoTracking()
                where d.DriverProfileId == driverProfileId
                join f in _db.FreightRequests.AsNoTracking() on d.FreightRequestId equals f.Id
                orderby d.CreatedAt descending, d.Id
                select new
                {
                    DeliveryId = d.Id,
                    d.FreightRequestId,
                    d.Status,
                    d.AgreedPrice,
                    f.ScheduledPickupAt,
                    TotalStops = _db.FreightStops.Count(s => s.FreightRequestId == f.Id),
                    CompletedStops = _db.FreightStops.Count(s => s.FreightRequestId == f.Id && s.CompletedAt != null),
                    d.CreatedAt,
                })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new DriverDeliverySummary(
                r.DeliveryId, r.FreightRequestId, r.Status, r.AgreedPrice, r.ScheduledPickupAt,
                r.TotalStops, r.CompletedStops, r.CreatedAt))
            .ToList();
    }

    public async Task<DeliveryRouteResponse?> GetRouteAsync(
        Guid driverProfileId, Guid deliveryId, RouteView view, CancellationToken cancellationToken)
    {
        var header = await (
                from d in _db.Deliveries.AsNoTracking()
                where d.Id == deliveryId && d.DriverProfileId == driverProfileId
                join f in _db.FreightRequests.AsNoTracking() on d.FreightRequestId equals f.Id
                select new
                {
                    DeliveryId = d.Id,
                    FreightId = f.Id,
                    f.CargoDescription,
                    f.CargoWeightKg,
                    TotalStops = _db.FreightStops.Count(s => s.FreightRequestId == f.Id),
                    CompletedStops = _db.FreightStops.Count(s => s.FreightRequestId == f.Id && s.CompletedAt != null),
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            return null;
        }

        // "Next" is decided by the database: first stop by sequence that has no completion time.
        var stopQuery = _db.FreightStops.AsNoTracking().Where(s => s.FreightRequestId == header.FreightId);
        if (view == RouteView.Next)
        {
            stopQuery = stopQuery.Where(s => s.CompletedAt == null);
        }

        var ordered =
            from s in stopQuery
            join a in _db.Addresses.AsNoTracking() on s.AddressId equals a.Id
            orderby s.Sequence
            select new
            {
                StopId = s.Id,
                s.Sequence,
                s.Type,
                s.Note,
                s.CompletedAt,
                Address = new AddressResponse(
                    a.Id, a.Label, a.Street, a.Number, a.Complement, a.District, a.City, a.State, a.ZipCode,
                    a.Latitude, a.Longitude),
            };

        if (view == RouteView.Next)
        {
            ordered = ordered.Take(1);
        }

        var stops = await ordered.ToListAsync(cancellationToken);
        var items = await FreightItemReader.LoadAsync(_db, new[] { header.FreightId }, cancellationToken);

        var response = stops
            .Select(s => new RouteStopResponse(
                s.StopId, s.Sequence, s.Type, s.Address, s.Note,
                items.TryGetValue(s.StopId, out var list) ? list : Array.Empty<FreightItemResponse>(),
                s.CompletedAt))
            .ToList();

        return new DeliveryRouteResponse(
            header.DeliveryId, header.FreightId, header.CargoDescription, header.CargoWeightKg, view,
            header.TotalStops, header.CompletedStops, response);
    }
}
