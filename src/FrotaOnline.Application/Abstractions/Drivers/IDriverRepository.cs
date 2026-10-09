using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Abstractions.Drivers;

public interface IDriverRepository
{
    Task<DriverProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<DriverProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DriverProfile>> ListByStatusAsync(DriverStatus status, CancellationToken cancellationToken);

    /// <summary>True when another user already registered this CPF or CNPJ.</summary>
    Task<bool> TaxIdInUseAsync(string taxId, Guid exceptUserId, CancellationToken cancellationToken);

    /// <summary>True when another user already registered this CNH.</summary>
    Task<bool> CnhInUseAsync(string cnhNumber, Guid exceptUserId, CancellationToken cancellationToken);

    Task AddAsync(DriverProfile profile, CancellationToken cancellationToken);
}
