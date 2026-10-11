using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Abstractions.Notifications;

public interface IDeviceTokenRepository
{
    Task<DeviceToken?> GetByTokenAsync(string token, CancellationToken cancellationToken);

    Task<IReadOnlyList<DeviceToken>> ListByUserIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    Task AddAsync(DeviceToken token, CancellationToken cancellationToken);

    void Remove(DeviceToken token);

    /// <summary>One DELETE statement for tokens the push provider reported as dead.</summary>
    Task<int> RemoveByTokensAsync(IReadOnlyCollection<string> tokens, CancellationToken cancellationToken);
}
