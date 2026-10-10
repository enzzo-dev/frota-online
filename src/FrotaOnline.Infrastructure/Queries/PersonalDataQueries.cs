using FrotaOnline.Application.Abstractions.Privacy;
using FrotaOnline.Application.Addresses.Dtos;
using FrotaOnline.Application.Drivers.Dtos;
using FrotaOnline.Application.Privacy.Dtos;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Queries;

/// <summary>One SQL projection per section of the export. Nothing is tracked and no entity is materialized.</summary>
internal sealed class PersonalDataQueries : IPersonalDataQueries
{
    private readonly AppDbContext _db;

    public PersonalDataQueries(AppDbContext db) => _db = db;

    public async Task<PersonalDataExport?> GetAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var account = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.AnonymizedAt == null)
            .Select(u => new { u.Id, u.FullName, u.Email, u.PhoneNumber, u.Role, u.CreatedAt, u.LastLoginAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (account is null)
        {
            return null;
        }

        var customerDocument = await _db.CustomerProfiles.AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => c.DocumentNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var driver = await _db.DriverProfiles.AsNoTracking()
            .Where(d => d.UserId == userId)
            .Select(d => new { d.Id, d.TaxId, d.CnhNumber, d.CnhCategory, d.Status, d.RejectionReason, d.ReviewedAt })
            .FirstOrDefaultAsync(cancellationToken);
        var driverId = driver?.Id;

        var vehicles = await _db.Vehicles.AsNoTracking()
            .Where(v => v.DriverProfileId == driverId)
            .OrderBy(v => v.CreatedAt)
            .Select(v => new VehicleResponse(v.Id, v.Type, v.Plate, v.Model, v.Year, v.CapacityKg, v.IsActive))
            .ToListAsync(cancellationToken);

        var addresses = await _db.Addresses.AsNoTracking()
            .Where(a => a.OwnerUserId == userId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new AddressResponse(
                a.Id, a.Label, a.Street, a.Number, a.Complement, a.District, a.City, a.State, a.ZipCode, a.Latitude, a.Longitude))
            .ToListAsync(cancellationToken);

        var files = await _db.MediaFiles.AsNoTracking()
            .Where(m => m.OwnerUserId == userId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new MediaFileResponse(m.Id, m.Kind, m.StorageKey, m.ContentType, m.SizeBytes, m.CreatedAt))
            .ToListAsync(cancellationToken);

        var consents = await (
                from c in _db.UserConsents.AsNoTracking()
                where c.UserId == userId
                join t in _db.TermDocuments.AsNoTracking() on c.TermDocumentId equals t.Id
                orderby c.AcceptedAt
                select new { t.Type, t.Version, c.AcceptedAt, c.IpAddress })
            .ToListAsync(cancellationToken);

        var freights = await _db.FreightRequests.AsNoTracking()
            .Where(f => f.CustomerId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new
            {
                f.Id,
                f.Status,
                f.CargoDescription,
                f.CargoWeightKg,
                StopCount = _db.FreightStops.Count(s => s.FreightRequestId == f.Id),
                f.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var proposals = await _db.Proposals.AsNoTracking()
            .Where(p => p.DriverProfileId == driverId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PersonalProposalData(p.Id, p.FreightRequestId, p.Price, p.Message, p.Status, p.CreatedAt))
            .ToListAsync(cancellationToken);

        var deliveries = await _db.Deliveries.AsNoTracking()
            .Where(d => d.CustomerId == userId || d.DriverProfileId == driverId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new
            {
                d.Id,
                d.FreightRequestId,
                AsCustomer = d.CustomerId == userId,
                d.Status,
                d.AgreedPrice,
                d.PlatformFee,
                d.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return new PersonalDataExport(
            utcNow,
            new PersonalAccountData(
                account.Id, account.FullName, account.Email, account.PhoneNumber, account.Role, account.CreatedAt, account.LastLoginAt),
            customerDocument,
            driver is null
                ? null
                : new PersonalDriverData(driver.TaxId, driver.CnhNumber, driver.CnhCategory, driver.Status, driver.RejectionReason, driver.ReviewedAt),
            vehicles,
            addresses,
            files,
            consents.Select(c => new PersonalConsentData(c.Type, c.Version, c.AcceptedAt, c.IpAddress)).ToList(),
            freights.Select(f => new PersonalFreightData(f.Id, f.Status, f.CargoDescription, f.CargoWeightKg, f.StopCount, f.CreatedAt)).ToList(),
            proposals,
            deliveries.Select(d => new PersonalDeliveryData(
                d.Id, d.FreightRequestId, d.AsCustomer ? "Customer" : "Driver", d.Status, d.AgreedPrice, d.PlatformFee, d.CreatedAt)).ToList());
    }
}
