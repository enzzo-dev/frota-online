using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Queries;

internal static class FreightItemReader
{
    /// <summary>Items of the given stops, grouped by stop id, read in one SQL statement over FreightItems alone.</summary>
    public static async Task<Dictionary<Guid, List<FreightItemResponse>>> LoadAsync(
        AppDbContext db, IReadOnlyCollection<Guid> stopIds, CancellationToken cancellationToken)
    {
        if (stopIds.Count == 0)
        {
            return new Dictionary<Guid, List<FreightItemResponse>>();
        }

        var rows = await db.FreightItems
            .AsNoTracking()
            .Where(i => stopIds.Contains(i.FreightStopId))
            .OrderBy(i => i.CreatedAt).ThenBy(i => i.Id)
            .Select(i => new { i.FreightStopId, i.Description, i.Quantity })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.FreightStopId)
            .ToDictionary(g => g.Key, g => g.Select(r => new FreightItemResponse(r.Description, r.Quantity)).ToList());
    }
}
