using FrotaOnline.Domain.Common;

namespace FrotaOnline.Domain.Entities;

public sealed class Address : BaseEntity
{
    private Address()
    {
    }

    public Guid OwnerUserId { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public string Street { get; private set; } = string.Empty;

    public string Number { get; private set; } = string.Empty;

    public string? Complement { get; private set; }

    public string District { get; private set; } = string.Empty;

    public string City { get; private set; } = string.Empty;

    public string State { get; private set; } = string.Empty;

    public string ZipCode { get; private set; } = string.Empty;

    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    /// <summary>Set when the customer deleted an address that past freights still point to. The data is kept for that history.</summary>
    public DateTime? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    /// <summary>Replaces every field. Coordinates not sent are cleared, so the caller sends them again.</summary>
    public void Update(
        string label, string street, string number, string? complement,
        string district, string city, string state, string zipCode, double? latitude, double? longitude)
    {
        if (IsDeleted)
        {
            throw new DomainException("Um endereço excluído não pode ser editado.");
        }

        Label = Guard.NotNullOrWhiteSpace(label, nameof(label));
        Street = Guard.NotNullOrWhiteSpace(street, nameof(street));
        Number = Guard.NotNullOrWhiteSpace(number, nameof(number));
        Complement = complement?.Trim();
        District = Guard.NotNullOrWhiteSpace(district, nameof(district));
        City = Guard.NotNullOrWhiteSpace(city, nameof(city));
        State = Guard.NotNullOrWhiteSpace(state, nameof(state)).ToUpperInvariant();
        ZipCode = Guard.NotNullOrWhiteSpace(zipCode, nameof(zipCode));
        Latitude = latitude;
        Longitude = longitude;
    }

    public void MarkDeleted(DateTime utcNow) => DeletedAt ??= utcNow;

    /// <summary>
    /// Keeps only city and state. Freight stops still point to this row, so it cannot be deleted,
    /// but nothing left can locate a home or a business.
    /// </summary>
    public void Anonymize()
    {
        Label = "Removed";
        Street = "Removed";
        Number = "0";
        Complement = null;
        District = "Removed";
        ZipCode = "00000000";
        Latitude = null;
        Longitude = null;
    }

    public static Address Create(
        Guid ownerUserId, string label, string street, string number, string? complement,
        string district, string city, string state, string zipCode, double? latitude, double? longitude) => new()
    {
        OwnerUserId = Guard.NotEmpty(ownerUserId, nameof(ownerUserId)),
        Label = Guard.NotNullOrWhiteSpace(label, nameof(label)),
        Street = Guard.NotNullOrWhiteSpace(street, nameof(street)),
        Number = Guard.NotNullOrWhiteSpace(number, nameof(number)),
        Complement = complement?.Trim(),
        District = Guard.NotNullOrWhiteSpace(district, nameof(district)),
        City = Guard.NotNullOrWhiteSpace(city, nameof(city)),
        State = Guard.NotNullOrWhiteSpace(state, nameof(state)).ToUpperInvariant(),
        ZipCode = Guard.NotNullOrWhiteSpace(zipCode, nameof(zipCode)),
        Latitude = latitude,
        Longitude = longitude
    };
}
