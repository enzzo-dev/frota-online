namespace FrotaOnline.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}
