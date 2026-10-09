using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Abstractions.Terms;

public interface IConsentRepository
{
    Task<IReadOnlyList<UserConsent>> ListByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task AddAsync(UserConsent consent, CancellationToken cancellationToken);
}
