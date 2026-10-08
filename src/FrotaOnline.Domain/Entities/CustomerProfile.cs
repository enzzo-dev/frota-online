using FrotaOnline.Domain.Common;

namespace FrotaOnline.Domain.Entities;

public sealed class CustomerProfile : BaseEntity, IAggregateRoot
{
    private CustomerProfile()
    {
    }

    public Guid UserId { get; private set; }

    /// <summary>CPF or CNPJ.</summary>
    public string DocumentNumber { get; private set; } = string.Empty;

    public static CustomerProfile Create(Guid userId, string documentNumber) => new()
    {
        UserId = Guard.NotEmpty(userId, nameof(userId)),
        DocumentNumber = Guard.NotNullOrWhiteSpace(documentNumber, nameof(documentNumber))
    };
}
