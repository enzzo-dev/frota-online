using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Abstractions.Marketplace;

public interface IDriverAgendaRepository
{
    /// <summary>True when the driver already has an active slot overlapping the interval.</summary>
    Task<bool> HasConflictAsync(Guid driverProfileId, DateTime startsAt, DateTime endsAt, CancellationToken cancellationToken);

    Task AddAsync(DriverAgendaSlot slot, CancellationToken cancellationToken);
}
