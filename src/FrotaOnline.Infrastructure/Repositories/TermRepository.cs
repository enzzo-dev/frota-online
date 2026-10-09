using FrotaOnline.Application.Abstractions.Terms;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class TermRepository : ITermRepository
{
    private readonly AppDbContext _db;

    public TermRepository(AppDbContext db) => _db = db;

    public Task<TermDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.TermDocuments.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TermDocument>> ListActiveAsync(CancellationToken cancellationToken) =>
        await _db.TermDocuments.Where(t => t.IsActive).OrderBy(t => t.Type).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TermDocument>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await _db.TermDocuments.Where(t => ids.Contains(t.Id)).ToListAsync(cancellationToken);

    public Task<bool> VersionExistsAsync(TermType type, string version, CancellationToken cancellationToken) =>
        _db.TermDocuments.AnyAsync(t => t.Type == type && t.Version == version, cancellationToken);

    public async Task AddAsync(TermDocument term, CancellationToken cancellationToken) =>
        await _db.TermDocuments.AddAsync(term, cancellationToken);
}
