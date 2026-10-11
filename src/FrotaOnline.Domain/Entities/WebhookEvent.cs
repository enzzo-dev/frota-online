using FrotaOnline.Domain.Common;

namespace FrotaOnline.Domain.Entities;

/// <summary>
/// An event already handled. The provider delivers each event at least once, so the event id is the idempotency key:
/// a second delivery finds the row and does nothing.
/// </summary>
public sealed class WebhookEvent : BaseEntity
{
    private WebhookEvent()
    {
    }

    public string EventId { get; private set; } = string.Empty;

    public string EventType { get; private set; } = string.Empty;

    public static WebhookEvent Create(string eventId, string eventType) => new()
    {
        EventId = Guard.NotNullOrWhiteSpace(eventId, nameof(eventId)),
        EventType = Guard.NotNullOrWhiteSpace(eventType, nameof(eventType))
    };
}
