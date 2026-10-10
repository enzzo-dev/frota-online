using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Queries;

internal static class FreightItemReader
{
    /// <summary>Items of every stop of the given freights, grouped by stop id, read in one SQL statement.</summary>
    public static async Task<Dictionary<Guid, List<FreightItemResponse>>> LoadAsync(
        AppDbContext db, IReadOnlyCollection<Guid> freightIds, CancellationToken cancellationToken)
    {
        var rows = await (
                from i in db.FreightItems.AsNoTracking()
                join s in db.FreightStops.AsNoTracking() on i.FreightStopId equals s.Id
                where freightIds.Contains(s.FreightRequestId)
                orderby i.CreatedAt, i.Id
                select new { i.FreightStopId, i.Description, i.Quantity })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.FreightStopId)
            .ToDictionary(g => g.Key, g => g.Select(r => new FreightItemResponse(r.Description, r.Quantity)).ToList());
    }
}
