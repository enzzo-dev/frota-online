using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Queries;

internal sealed class DriverMetricsQueries : IDriverMetricsQueries
{
    private readonly AppDbContext _db;

    public DriverMetricsQueries(AppDbContext db) => _db = db;

    public async Task<DriverMetricsResponse> GetAsync(
        Guid driverProfileId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var views = _db.FreightViews.AsNoTracking()
            .Where(v => v.DriverProfileId == driverProfileId
                        && (from == null || v.FirstViewedAt >= from)
                        && (to == null || v.FirstViewedAt <= to));

        var seen = await views.CountAsync(cancellationToken);
        var missed = await views.CountAsync(
            v => _db.FreightRequests.Any(f => f.Id == v.FreightRequestId
                                              && (f.Status == FreightStatus.Approved || f.Status == FreightStatus.Expired))
                 && !_db.Proposals.Any(p => p.FreightRequestId == v.FreightRequestId && p.DriverProfileId == driverProfileId),
            cancellationToken);

        var proposals = await _db.Proposals.AsNoTracking()
            .Where(p => p.DriverProfileId == driverProfileId
                        && (from == null || p.CreatedAt >= from)
                        && (to == null || p.CreatedAt <= to))
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var completed = await _db.Deliveries.AsNoTracking()
            .CountAsync(d => d.DriverProfileId == driverProfileId && d.Status == DeliveryStatus.Delivered
                             && (from == null || d.DeliveredAt >= from) && (to == null || d.DeliveredAt <= to),
                cancellationToken);
        var cancelled = await _db.Deliveries.AsNoTracking()
            .CountAsync(d => d.DriverProfileId == driverProfileId && d.Status == DeliveryStatus.Cancelled
                             && (from == null || d.CancelledAt >= from) && (to == null || d.CancelledAt <= to),
                cancellationToken);

        int Of(ProposalStatus status) => proposals.FirstOrDefault(x => x.Status == status)?.Count ?? 0;
        var sent = proposals.Sum(x => x.Count);
        var accepted = Of(ProposalStatus.Accepted);
        var rate = sent == 0 ? 0m : Math.Round((decimal)accepted / sent, 4);

        return new DriverMetricsResponse(
            seen, missed, sent, accepted, Of(ProposalStatus.Rejected), Of(ProposalStatus.Expired), rate, completed, cancelled);
    }
}
