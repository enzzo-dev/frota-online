using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Domain.Entities;

public sealed class Vehicle : BaseEntity
{
    private Vehicle()
    {
    }

    public Guid DriverProfileId { get; private set; }

    public VehicleType Type { get; private set; }

    public string Plate { get; private set; } = string.Empty;

    public string Model { get; private set; } = string.Empty;

    public int Year { get; private set; }

    public decimal CapacityKg { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static Vehicle Create(Guid driverProfileId, VehicleType type, string plate, string model, int year, decimal capacityKg) => new()
    {
        DriverProfileId = Guard.NotEmpty(driverProfileId, nameof(driverProfileId)),
        Type = type,
        Plate = Guard.NotNullOrWhiteSpace(plate, nameof(plate)).ToUpperInvariant(),
        Model = Guard.NotNullOrWhiteSpace(model, nameof(model)),
        Year = year,
        CapacityKg = Guard.Positive(capacityKg, nameof(capacityKg))
    };

    public void Deactivate() => IsActive = false;
}
