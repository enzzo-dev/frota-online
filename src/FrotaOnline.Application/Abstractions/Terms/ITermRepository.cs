using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Abstractions.Terms;

public interface ITermRepository
{
    Task<TermDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<TermDocument>> ListActiveAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<TermDocument>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<bool> VersionExistsAsync(TermType type, string version, CancellationToken cancellationToken);

    Task AddAsync(TermDocument term, CancellationToken cancellationToken);
}
