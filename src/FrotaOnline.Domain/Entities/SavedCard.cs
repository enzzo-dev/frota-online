using FrotaOnline.Domain.Common;

namespace FrotaOnline.Domain.Entities;

/// <summary>
/// A card the customer chose to keep for the next freights. Only the provider's token is stored, never the number or the
/// security code: the token works only for the provider customer it was created for.
/// </summary>
public sealed class SavedCard : BaseEntity, IAggregateRoot
{
    public const int MaxPerUser = 5;

    private SavedCard()
    {
    }

    public Guid UserId { get; private set; }

    public string GatewayCustomerId { get; private set; } = string.Empty;

    public string Token { get; private set; } = string.Empty;

    public string Brand { get; private set; } = string.Empty;

    /// <summary>Last four digits, to tell the cards apart on screen.</summary>
    public string Last4 { get; private set; } = string.Empty;

    public static SavedCard Create(Guid userId, string gatewayCustomerId, string token, string brand, string last4) => new()
    {
        UserId = Guard.NotEmpty(userId, nameof(userId)),
        GatewayCustomerId = Guard.NotNullOrWhiteSpace(gatewayCustomerId, nameof(gatewayCustomerId)),
        Token = Guard.NotNullOrWhiteSpace(token, nameof(token)),
        Brand = string.IsNullOrWhiteSpace(brand) ? "CARD" : brand.Trim().ToUpperInvariant(),
        Last4 = string.IsNullOrWhiteSpace(last4) ? "----" : last4.Trim()
    };
}
