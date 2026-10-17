using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Repositories;

internal sealed class SignatureRepository : ISignatureRepository
{
    private readonly AppDbContext _db;

    public SignatureRepository(AppDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid freightRequestId, SignatureType type, CancellationToken cancellationToken) =>
        _db.Signatures.AnyAsync(s => s.FreightRequestId == freightRequestId && s.Type == type, cancellationToken);

    public Task<bool> StoragePathExistsAsync(string storagePath, CancellationToken cancellationToken) =>
        _db.Signatures.AnyAsync(s => s.StoragePath == storagePath, cancellationToken);

    public async Task AddAsync(Signature signature, CancellationToken cancellationToken) =>
        await _db.Signatures.AddAsync(signature, cancellationToken);
}
