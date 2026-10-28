using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Addresses.Dtos;
using FrotaOnline.Application.Deliveries.Dtos;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Queries;

internal sealed class DeliveryQueries : IDeliveryQueries
{
    private readonly AppDbContext _db;

    public DeliveryQueries(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<DriverDeliverySummary>> ListByDriverAsync(
        Guid driverProfileId, CancellationToken cancellationToken) =>
        await DriverSummaries(driverProfileId, deliveryId: null, cancellationToken);

    public async Task<DriverDeliverySummary?> GetSummaryByDriverAsync(
        Guid driverProfileId, Guid deliveryId, CancellationToken cancellationToken) =>
        (await DriverSummaries(driverProfileId, deliveryId, cancellationToken)).FirstOrDefault();

    private async Task<List<DriverDeliverySummary>> DriverSummaries(
        Guid driverProfileId, Guid? deliveryId, CancellationToken cancellationToken)
    {
        var rows = await (
                from d in _db.Deliveries.AsNoTracking()
                where d.DriverProfileId == driverProfileId && (deliveryId == null || d.Id == deliveryId)
                join f in _db.FreightRequests.AsNoTracking() on d.FreightRequestId equals f.Id
                join v in _db.Vehicles.AsNoTracking() on d.VehicleId equals v.Id
                join c in _db.Users.AsNoTracking() on d.CustomerId equals c.Id
                orderby d.CreatedAt descending, d.Id
                select new
                {
                    DeliveryId = d.Id,
                    d.FreightRequestId,
                    d.Status,
                    d.AgreedPrice,
                    d.PlatformFee,
                    d.DeliveredAt,
                    f.ScheduledPickupAt,
                    f.CargoDescription,
                    f.CargoWeightKg,
                    f.RouteDistanceMeters,
                    f.RouteDurationSeconds,
                    VehiclePlate = v.Plate,
                    VehicleModel = v.Model,
                    CustomerName = c.FullName,
                    TotalStops = _db.FreightStops.Count(s => s.FreightRequestId == f.Id),
                    CompletedStops = _db.FreightStops.Count(s => s.FreightRequestId == f.Id && s.CompletedAt != null),
                    Payment = _db.Payments.Where(x => x.DeliveryId == d.Id).Select(x => (PaymentStatus?)x.Status).FirstOrDefault(),
                    CustomerSigned = _db.Signatures.Any(x => x.FreightRequestId == f.Id && x.Type == SignatureType.Customer),
                    DriverSigned = _db.Signatures.Any(x => x.FreightRequestId == f.Id && x.Type == SignatureType.Driver),
                    d.CreatedAt,
                })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return new List<DriverDeliverySummary>();
        }

        var deliveryIds = rows.Select(r => r.DeliveryId).ToList();
        var freightIds = rows.Select(r => r.FreightRequestId).ToList();

        var slots = (await _db.DriverAgendaSlots.AsNoTracking()
                .Where(x => deliveryIds.Contains(x.DeliveryId))
                .Select(x => new { x.DeliveryId, x.StartsAt, x.EndsAt, x.CreatedAt })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.DeliveryId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAt).First());

        var places = (await (
                    from s in _db.FreightStops.AsNoTracking()
                    where freightIds.Contains(s.FreightRequestId)
                    join a in _db.Addresses.AsNoTracking() on s.AddressId equals a.Id
                    select new { s.FreightRequestId, s.Sequence, a.District, a.City })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.FreightRequestId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Sequence).ToList());

        return rows
            .Select(r =>
            {
                slots.TryGetValue(r.DeliveryId, out var slot);
                places.TryGetValue(r.FreightRequestId, out var stops);
                var origin = stops?.FirstOrDefault();
                var destination = stops?.LastOrDefault();
                return new DriverDeliverySummary(
                    r.DeliveryId, r.FreightRequestId, r.Status, r.AgreedPrice, r.ScheduledPickupAt,
                    r.TotalStops, r.CompletedStops, r.Payment, r.CustomerSigned, r.DriverSigned, r.CreatedAt,
                    slot?.StartsAt, slot?.EndsAt, r.CargoDescription, r.CargoWeightKg,
                    origin?.District, origin?.City, destination?.District, destination?.City,
                    r.RouteDistanceMeters, r.RouteDurationSeconds, r.VehiclePlate, r.VehicleModel,
                    FirstName(r.CustomerName), r.DeliveredAt, r.PlatformFee, r.AgreedPrice - r.PlatformFee);
            })
            .ToList();
    }

    private static string FirstName(string fullName) =>
        fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? fullName;

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
                    f.DriverNote,
                    HasCargoPhoto = _db.FreightCargoPhotos.Any(p => p.FreightRequestId == f.Id),
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
        var items = await FreightItemReader.LoadAsync(_db, stops.Select(s => s.StopId).ToList(), cancellationToken);

        var response = stops
            .Select(s => new RouteStopResponse(
                s.StopId, s.Sequence, s.Type, s.Address, s.Note,
                items.TryGetValue(s.StopId, out var list) ? list : Array.Empty<FreightItemResponse>(),
                s.CompletedAt))
            .ToList();

        return new DeliveryRouteResponse(
            header.DeliveryId, header.FreightId, header.CargoDescription, header.CargoWeightKg, view,
            header.TotalStops, header.CompletedStops, response, header.DriverNote, header.HasCargoPhoto);
    }

    public async Task<IReadOnlyList<CustomerDeliverySummary>> ListByCustomerAsync(
        Guid customerId, CancellationToken cancellationToken)
    {
        var rows = await (
                from d in _db.Deliveries.AsNoTracking()
                where d.CustomerId == customerId
                join p in _db.DriverProfiles.AsNoTracking() on d.DriverProfileId equals p.Id
                join u in _db.Users.AsNoTracking() on p.UserId equals u.Id
                join v in _db.Vehicles.AsNoTracking() on d.VehicleId equals v.Id
                orderby d.CreatedAt descending, d.Id
                select new
                {
                    DeliveryId = d.Id,
                    d.FreightRequestId,
                    d.Status,
                    d.AgreedPrice,
                    DriverName = u.FullName,
                    VehicleType = v.Type,
                    VehicleModel = v.Model,
                    VehiclePlate = v.Plate,
                    TotalStops = _db.FreightStops.Count(s => s.FreightRequestId == d.FreightRequestId),
                    CompletedStops = _db.FreightStops.Count(s => s.FreightRequestId == d.FreightRequestId && s.CompletedAt != null),
                    Payment = _db.Payments.Where(x => x.DeliveryId == d.Id).Select(x => (PaymentStatus?)x.Status).FirstOrDefault(),
                    d.CreatedAt,
                })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new CustomerDeliverySummary(
                r.DeliveryId, r.FreightRequestId, r.Status, r.AgreedPrice, r.DriverName, r.VehicleType,
                r.VehicleModel, r.VehiclePlate, r.TotalStops, r.CompletedStops, r.Payment, r.CreatedAt))
            .ToList();
    }

    public async Task<DeliveryStatusResponse?> GetStatusForCustomerAsync(
        Guid customerId, Guid deliveryId, CancellationToken cancellationToken)
    {
        // One round trip, no joins to users, vehicles or addresses: only counters and the newest event time.
        var row = await (
                from d in _db.Deliveries.AsNoTracking()
                where d.Id == deliveryId && d.CustomerId == customerId
                select new
                {
                    d.Status,
                    TotalStops = _db.FreightStops.Count(s => s.FreightRequestId == d.FreightRequestId),
                    CompletedStops = _db.FreightStops.Count(s => s.FreightRequestId == d.FreightRequestId && s.CompletedAt != null),
                    Payment = _db.Payments.Where(x => x.DeliveryId == d.Id).Select(x => (PaymentStatus?)x.Status).FirstOrDefault(),
                    LastEventAt = _db.DeliveryEvents.Where(e => e.DeliveryId == d.Id).Max(e => (DateTime?)e.OccurredAt),
                    CustomerSigned = _db.Signatures.Any(x => x.FreightRequestId == d.FreightRequestId && x.Type == SignatureType.Customer),
                    DriverSigned = _db.Signatures.Any(x => x.FreightRequestId == d.FreightRequestId && x.Type == SignatureType.Driver),
                    Location = _db.DeliveryLocations
                        .Where(l => l.DeliveryId == d.Id)
                        .Select(l => new { l.Latitude, l.Longitude, l.Heading, l.SpeedKmh, l.RecordedAt })
                        .FirstOrDefault(),
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var location = row.Location is { } l
                       && row.Status is DeliveryStatus.ToPickup or DeliveryStatus.PickedUp or DeliveryStatus.InTransit
            ? new DriverLocationResponse(l.Latitude, l.Longitude, l.Heading, l.SpeedKmh, l.RecordedAt)
            : null;

        return DeliveryStatusResponse.Create(
            deliveryId, row.Status, row.TotalStops, row.CompletedStops, row.Payment, row.LastEventAt, location,
            row.CustomerSigned, row.DriverSigned);
    }

    public async Task<CustomerDeliveryDetail?> GetForCustomerAsync(
        Guid customerId, Guid deliveryId, CancellationToken cancellationToken)
    {
        var header = await (
                from d in _db.Deliveries.AsNoTracking()
                where d.Id == deliveryId && d.CustomerId == customerId
                join p in _db.DriverProfiles.AsNoTracking() on d.DriverProfileId equals p.Id
                join u in _db.Users.AsNoTracking() on p.UserId equals u.Id
                join v in _db.Vehicles.AsNoTracking() on d.VehicleId equals v.Id
                select new
                {
                    DeliveryId = d.Id,
                    d.FreightRequestId,
                    d.Status,
                    d.AgreedPrice,
                    DriverName = u.FullName,
                    VehicleType = v.Type,
                    VehicleModel = v.Model,
                    VehiclePlate = v.Plate,
                    Payment = _db.Payments.Where(x => x.DeliveryId == d.Id).Select(x => (PaymentStatus?)x.Status).FirstOrDefault(),
                    d.PickedUpAt,
                    d.DeliveredAt,
                    d.CancelledAt,
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            return null;
        }

        var stops = await (
                from s in _db.FreightStops.AsNoTracking()
                where s.FreightRequestId == header.FreightRequestId
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
                })
            .ToListAsync(cancellationToken);

        var items = await FreightItemReader.LoadAsync(_db, stops.Select(s => s.StopId).ToList(), cancellationToken);

        var events = await _db.DeliveryEvents.AsNoTracking()
            .Where(e => e.DeliveryId == deliveryId)
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.CreatedAt)
            .Select(e => new { e.Status, e.OccurredAt, e.Note, e.Latitude, e.Longitude, HasProof = e.MediaFileId != null })
            .ToListAsync(cancellationToken);

        return new CustomerDeliveryDetail(
            header.DeliveryId, header.FreightRequestId, header.Status, header.AgreedPrice, header.DriverName,
            header.VehicleType, header.VehicleModel, header.VehiclePlate, header.Payment,
            header.PickedUpAt, header.DeliveredAt, header.CancelledAt,
            stops.Select(s => new FreightStopResponse(
                s.StopId, s.Sequence, s.Type, s.Address, s.Note,
                items.TryGetValue(s.StopId, out var list) ? list : Array.Empty<FreightItemResponse>(),
                s.CompletedAt)).ToList(),
            events.Select(e => new DeliveryEventResponse(e.Status, e.OccurredAt, e.Note, e.Latitude, e.Longitude, e.HasProof)).ToList());
    }
}
