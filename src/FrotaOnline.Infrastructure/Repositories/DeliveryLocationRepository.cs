using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class DeliveryLocationRepository : IDeliveryLocationRepository
{
    private static readonly string ToPickup = DeliveryStatus.ToPickup.ToString();
    private static readonly string PickedUp = DeliveryStatus.PickedUp.ToString();
    private static readonly string InTransit = DeliveryStatus.InTransit.ToString();

    private readonly AppDbContext _db;

    public DeliveryLocationRepository(AppDbContext db) => _db = db;

    public Task<DeliveryLocation?> GetByDeliveryIdAsync(Guid deliveryId, CancellationToken cancellationToken) =>
        _db.DeliveryLocations.FirstOrDefaultAsync(l => l.DeliveryId == deliveryId, cancellationToken);

    public async Task AddAsync(DeliveryLocation location, CancellationToken cancellationToken) =>
        await _db.DeliveryLocations.AddAsync(location, cancellationToken);

    public void Remove(DeliveryLocation location) => _db.DeliveryLocations.Remove(location);

    public async Task<bool> TryRecordForDriverAsync(
        Guid driverUserId, DeliveryLocation location, CancellationToken cancellationToken)
    {
        var affected = await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "DeliveryLocations"
                ("Id", "DeliveryId", "Latitude", "Longitude", "Heading", "SpeedKmh", "RecordedAt", "CreatedAt", "UpdatedAt")
            SELECT {location.Id}, d."Id", {location.Latitude}, {location.Longitude}, CAST({location.Heading} AS double precision), CAST({location.SpeedKmh} AS double precision),
                   {location.RecordedAt}, {location.RecordedAt}, NULL
            FROM "Deliveries" d
            JOIN "DriverProfiles" p ON p."Id" = d."DriverProfileId"
            WHERE d."Id" = {location.DeliveryId}
              AND p."UserId" = {driverUserId}
              AND d."Status" IN ({ToPickup}, {PickedUp}, {InTransit})
            ON CONFLICT ("DeliveryId") DO UPDATE SET
                "Latitude" = EXCLUDED."Latitude",
                "Longitude" = EXCLUDED."Longitude",
                "Heading" = EXCLUDED."Heading",
                "SpeedKmh" = EXCLUDED."SpeedKmh",
                "RecordedAt" = EXCLUDED."RecordedAt",
                "UpdatedAt" = EXCLUDED."RecordedAt"
            """, cancellationToken);

        return affected > 0;
    }
}
