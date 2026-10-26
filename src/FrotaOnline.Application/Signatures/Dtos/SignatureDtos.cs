using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Signatures.Dtos;

public sealed record RegisterSignatureRequest(SignatureType Type, Guid FreightId, string Image);

public sealed record SignatureResponse(
    Guid Id, SignatureType Type, Guid FreightId, Guid CustomerId, Guid DriverProfileId, string ContentType, int SizeBytes, DateTime CreatedAt);

public sealed record SignatureImage(string ContentType, byte[] Content);
