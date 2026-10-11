using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Queries;

internal sealed class AddressUsageQueries : IAddressUsageQueries
{
    private readonly AppDbContext _db;

    public AddressUsageQueries(AppDbContext db) => _db = db;

    public async Task<AddressUsage> GetUsageAsync(Guid addressId, CancellationToken cancellationToken)
    {
        // The freights that have a stop at this address.
        var freights =
            from s in _db.FreightStops.AsNoTracking()
            where s.AddressId == addressId
            join f in _db.FreightRequests.AsNoTracking() on s.FreightRequestId equals f.Id
            select f;

        // Open freight, or approved one whose delivery has not ended yet.
        var active = await freights.AnyAsync(
            f => f.Status == FreightStatus.Open
                 || (f.Status == FreightStatus.Approved
                     && _db.Deliveries.Any(d => d.FreightRequestId == f.Id
                                                && d.Status != DeliveryStatus.Delivered
                                                && d.Status != DeliveryStatus.Cancelled)),
            cancellationToken);
        if (active)
        {
            return AddressUsage.Active;
        }

        return await freights.AnyAsync(cancellationToken) ? AddressUsage.History : AddressUsage.None;
    }
}
