namespace FrotaOnline.Application.Addresses.Dtos;

public sealed record CreateAddressRequest(
    string Label,
    string Street,
    string Number,
    string? Complement,
    string District,
    string City,
    string State,
    string ZipCode,
    double? Latitude,
    double? Longitude);

public sealed record AddressResponse(
    Guid Id,
    string Label,
    string Street,
    string Number,
    string? Complement,
    string District,
    string City,
    string State,
    string ZipCode,
    double? Latitude,
    double? Longitude);
