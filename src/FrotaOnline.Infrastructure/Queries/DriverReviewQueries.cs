using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Admin.Dtos;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Queries;

internal sealed class DriverReviewQueries : IDriverReviewQueries
{
    private readonly AppDbContext _db;

    public DriverReviewQueries(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<DriverReviewSummary>> ListByStatusAsync(
        DriverStatus status, CancellationToken cancellationToken)
    {
        var rows = await (
                from d in _db.DriverProfiles.AsNoTracking()
                where d.Status == status
                join u in _db.Users.AsNoTracking() on d.UserId equals u.Id
                orderby d.UpdatedAt ?? d.CreatedAt, d.Id
                select new
                {
                    DriverId = d.Id,
                    UserId = u.Id,
                    u.FullName,
                    u.Email,
                    u.PhoneNumber,
                    d.Status,
                    LastUpdatedAt = d.UpdatedAt ?? d.CreatedAt,
                })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new DriverReviewSummary(
                r.DriverId, r.UserId, r.FullName, r.Email, r.PhoneNumber, r.Status, r.LastUpdatedAt))
            .ToList();
    }
}
