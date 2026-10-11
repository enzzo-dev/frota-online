using FrotaOnline.Application.Common;
using FrotaOnline.Application.Notifications.Dtos;

namespace FrotaOnline.Application.Notifications;

public interface IDeviceService
{
    /// <summary>Safe to call at every app start: a known token only has its owner and last-seen time refreshed.</summary>
    Task<Result> RegisterAsync(Guid userId, RegisterDeviceRequest request, CancellationToken cancellationToken);

    /// <summary>Call on logout. Idempotent: an unknown token (or one of someone else) is ignored.</summary>
    Task<Result> UnregisterAsync(Guid userId, string? token, CancellationToken cancellationToken);
}
