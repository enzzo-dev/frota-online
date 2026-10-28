using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class FreightViewRepository : IFreightViewRepository
{
    private readonly AppDbContext _db;

    public FreightViewRepository(AppDbContext db) => _db = db;

    public async Task RecordAsync(Guid freightRequestId, Guid driverProfileId, DateTime utcNow, CancellationToken cancellationToken) =>
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "FreightViews" ("Id", "FreightRequestId", "DriverProfileId", "FirstViewedAt", "LastViewedAt", "ViewCount", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {freightRequestId}, {driverProfileId}, {utcNow}, {utcNow}, 1, {utcNow})
            ON CONFLICT ("FreightRequestId", "DriverProfileId") DO UPDATE
            SET "LastViewedAt" = {utcNow}, "ViewCount" = "FreightViews"."ViewCount" + 1, "UpdatedAt" = {utcNow}
            """,
            cancellationToken);
}
