using FrotaOnline.Application.Addresses.Dtos;
using FrotaOnline.Application.Addresses.Validators;

namespace FrotaOnline.Tests.Addresses;

public class CreateAddressRequestValidatorTests
{
    private static CreateAddressRequest Request(double? latitude, double? longitude) =>
        new("Casa", "Rua A", "10", null, "Centro", "Sao Paulo", "SP", "01001000", latitude, longitude);

    private readonly CreateAddressRequestValidator _validator = new();

    [Theory]
    [InlineData(null, null)]
    [InlineData(-23.55, -46.63)]
    public void CoordinatesAbsentOrComplete_AreValid(double? latitude, double? longitude) =>
        Assert.True(_validator.Validate(Request(latitude, longitude)).IsValid);

    [Theory]
    [InlineData(-23.55, null)]
    [InlineData(null, -46.63)]
    public void OnlyOneCoordinate_IsRejected(double? latitude, double? longitude)
    {
        var result = _validator.Validate(Request(latitude, longitude));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Send latitude and longitude together.");
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(0, -181)]
    public void CoordinatesOutOfRange_AreRejected(double latitude, double longitude) =>
        Assert.False(_validator.Validate(Request(latitude, longitude)).IsValid);
}
