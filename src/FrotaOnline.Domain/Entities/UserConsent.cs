using FrotaOnline.Domain.Common;

namespace FrotaOnline.Domain.Entities;

public sealed class UserConsent : BaseEntity
{
    private UserConsent()
    {
    }

    public Guid UserId { get; private set; }

    public Guid TermDocumentId { get; private set; }

    public DateTime AcceptedAt { get; private set; }

    public string? IpAddress { get; private set; }

    public static UserConsent Create(Guid userId, Guid termDocumentId, DateTime acceptedAt, string? ipAddress) => new()
    {
        UserId = Guard.NotEmpty(userId, nameof(userId)),
        TermDocumentId = Guard.NotEmpty(termDocumentId, nameof(termDocumentId)),
        AcceptedAt = acceptedAt,
        IpAddress = ipAddress
    };
}
