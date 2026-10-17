using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Signatures.Dtos;

public sealed record CreateSignatureUploadUrlRequest(SignatureType Type, Guid FreightId, string ContentType);

public sealed record SignatureUploadUrlResponse(
    string StoragePath, string UploadUrl, string Method, IReadOnlyDictionary<string, string> Headers, long MaxBytes, DateTime ExpiresAt);

public sealed record RegisterSignatureRequest(SignatureType Type, Guid FreightId, string StoragePath);

public sealed record SignatureResponse(
    Guid Id, SignatureType Type, Guid FreightId, Guid CustomerId, Guid DriverProfileId, string StoragePath, DateTime CreatedAt);
