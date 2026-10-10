using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class DriverAgendaRepository : IDriverAgendaRepository
{
    private readonly AppDbContext _db;

    public DriverAgendaRepository(AppDbContext db) => _db = db;

    public Task<bool> HasConflictAsync(Guid driverProfileId, DateTime startsAt, DateTime endsAt, CancellationToken cancellationToken) =>
        _db.DriverAgendaSlots.AnyAsync(
            s => s.DriverProfileId == driverProfileId && s.ReleasedAt == null && startsAt < s.EndsAt && endsAt > s.StartsAt,
            cancellationToken);

    public async Task AddAsync(DriverAgendaSlot slot, CancellationToken cancellationToken) =>
        await _db.DriverAgendaSlots.AddAsync(slot, cancellationToken);
}
