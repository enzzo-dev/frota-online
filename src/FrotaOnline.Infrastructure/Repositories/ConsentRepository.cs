using FrotaOnline.Application.Abstractions.Terms;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class ConsentRepository : IConsentRepository
{
    private readonly AppDbContext _db;

    public ConsentRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<UserConsent>> ListByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await _db.UserConsents.Where(c => c.UserId == userId).ToListAsync(cancellationToken);

    public async Task AddAsync(UserConsent consent, CancellationToken cancellationToken) =>
        await _db.UserConsents.AddAsync(consent, cancellationToken);
}
