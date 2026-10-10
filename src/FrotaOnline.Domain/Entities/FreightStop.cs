using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Domain.Entities;

/// <summary>Cargo that is collected or handed over at one stop.</summary>
public sealed record FreightItemDraft(string Description, int Quantity);

/// <summary>A stop as the customer described it. The list order is the order the driver follows.</summary>
public sealed record FreightStopDraft(StopType Type, Guid AddressId, string? Note, IReadOnlyList<FreightItemDraft>? Items);

/// <summary>A pickup or delivery point of a freight. The last stop is the final delivery.</summary>
public sealed class FreightStop : BaseEntity
{
    private readonly List<FreightItem> _items = new();

    private FreightStop()
    {
    }

    public Guid FreightRequestId { get; private set; }

    /// <summary>Order in the route, starting at 1.</summary>
    public int Sequence { get; private set; }

    public StopType Type { get; private set; }

    public Guid AddressId { get; private set; }

    public string? Note { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public IReadOnlyList<FreightItem> Items => _items;

    public bool IsPickup => Type == StopType.Pickup;

    public bool IsCompleted => CompletedAt is not null;

    internal static FreightStop Create(Guid freightRequestId, int sequence, FreightStopDraft draft)
    {
        var stop = new FreightStop
        {
            FreightRequestId = Guard.NotEmpty(freightRequestId, nameof(freightRequestId)),
            Sequence = sequence,
            Type = draft.Type,
            AddressId = Guard.NotEmpty(draft.AddressId, nameof(draft.AddressId)),
            Note = string.IsNullOrWhiteSpace(draft.Note) ? null : draft.Note.Trim()
        };

        var items = draft.Items ?? Array.Empty<FreightItemDraft>();
        if (items.Count > FreightLimits.MaxItemsPerStop)
        {
            throw new DomainException($"A stop can have at most {FreightLimits.MaxItemsPerStop} items.");
        }

        foreach (var item in items)
        {
            stop._items.Add(FreightItem.Create(stop.Id, item.Description, item.Quantity));
        }

        return stop;
    }

    public void Complete(DateTime utcNow)
    {
        if (IsCompleted)
        {
            throw new DomainException("This stop was already completed.");
        }

        CompletedAt = utcNow;
    }
}
