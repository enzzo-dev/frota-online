using FrotaOnline.Domain.Common;

namespace FrotaOnline.Domain.Entities;

/// <summary>One line of cargo that goes to a given stop, such as "Brake pads x 12".</summary>
public sealed class FreightItem : BaseEntity
{
    private FreightItem()
    {
    }

    public Guid FreightStopId { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    internal static FreightItem Create(Guid freightStopId, string description, int quantity)
    {
        if (quantity < 1)
        {
            throw new DomainException("A quantidade do item deve ser de pelo menos 1.");
        }

        return new FreightItem
        {
            FreightStopId = Guard.NotEmpty(freightStopId, nameof(freightStopId)),
            Description = Guard.NotNullOrWhiteSpace(description, nameof(description)),
            Quantity = quantity
        };
    }
}
