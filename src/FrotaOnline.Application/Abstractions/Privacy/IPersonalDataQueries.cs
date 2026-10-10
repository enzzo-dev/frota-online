using FrotaOnline.Application.Privacy.Dtos;

namespace FrotaOnline.Application.Abstractions.Privacy;

public interface IPersonalDataQueries
{
    /// <summary>Null when the user does not exist. Every part is read with its own SQL projection.</summary>
    Task<PersonalDataExport?> GetAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken);
}
