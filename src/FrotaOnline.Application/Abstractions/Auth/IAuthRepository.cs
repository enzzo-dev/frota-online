using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Abstractions.Auth;

/// <summary>Persistence needed by the authentication module (refresh token sessions).</summary>
public interface IAuthRepository
{
    Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> GetActiveRefreshTokensByUserAsync(
        Guid userId, DateTime utcNow, CancellationToken cancellationToken);

    Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken);
}
