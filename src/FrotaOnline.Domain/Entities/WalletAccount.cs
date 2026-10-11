using FrotaOnline.Domain.Common;

namespace FrotaOnline.Domain.Entities;

/// <summary>
/// The driver's Mercado Pago account, authorised with OAuth. Wallet payments (Apple Pay) are created with these tokens so the
/// money goes to the driver and the platform only keeps its fee. The tokens arrive here already encrypted.
/// </summary>
public sealed class WalletAccount : BaseEntity, IAggregateRoot
{
    private WalletAccount()
    {
    }

    public Guid DriverProfileId { get; private set; }

    public string ProviderUserId { get; private set; } = string.Empty;

    public string AccessTokenProtected { get; private set; } = string.Empty;

    public string RefreshTokenProtected { get; private set; } = string.Empty;

    public DateTime ExpiresAt { get; private set; }

    public static WalletAccount Create(
        Guid driverProfileId, string providerUserId, string accessTokenProtected, string refreshTokenProtected, DateTime expiresAt) => new()
    {
        DriverProfileId = Guard.NotEmpty(driverProfileId, nameof(driverProfileId)),
        ProviderUserId = Guard.NotNullOrWhiteSpace(providerUserId, nameof(providerUserId)),
        AccessTokenProtected = Guard.NotNullOrWhiteSpace(accessTokenProtected, nameof(accessTokenProtected)),
        RefreshTokenProtected = Guard.NotNullOrWhiteSpace(refreshTokenProtected, nameof(refreshTokenProtected)),
        ExpiresAt = expiresAt
    };

    public void UpdateTokens(string providerUserId, string accessTokenProtected, string refreshTokenProtected, DateTime expiresAt)
    {
        ProviderUserId = Guard.NotNullOrWhiteSpace(providerUserId, nameof(providerUserId));
        AccessTokenProtected = Guard.NotNullOrWhiteSpace(accessTokenProtected, nameof(accessTokenProtected));
        RefreshTokenProtected = Guard.NotNullOrWhiteSpace(refreshTokenProtected, nameof(refreshTokenProtected));
        ExpiresAt = expiresAt;
    }

    /// <summary>Within a day of expiring: refresh before using it.</summary>
    public bool NeedsRefresh(DateTime utcNow) => utcNow >= ExpiresAt.AddDays(-1);
}
