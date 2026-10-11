using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Queries;

internal sealed class MatchingDriverQueries : IMatchingDriverQueries
{
    private readonly AppDbContext _db;

    public MatchingDriverQueries(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Guid>> ListUserIdsForNewFreightAsync(
        FreightMatchCriteria criteria, int limit, CancellationToken cancellationToken) =>
        await (from d in _db.DriverProfiles.AsNoTracking()
               where d.Status == DriverStatus.Approved
                     && d.UserId != criteria.CustomerId
                     && _db.Vehicles.Any(v => v.DriverProfileId == d.Id
                                              && v.IsActive
                                              && v.Type == criteria.VehicleType
                                              && v.CapacityKg >= criteria.CargoWeightKg)
                     && !_db.DriverAgendaSlots.Any(s => s.DriverProfileId == d.Id
                                                        && s.ReleasedAt == null
                                                        && criteria.StartsAt < s.EndsAt
                                                        && criteria.EndsAt > s.StartsAt)
                     && !_db.NotificationPreferences.Any(p => p.UserId == d.UserId && !p.NewFreightAlerts)
               orderby d.CreatedAt
               select d.UserId)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
