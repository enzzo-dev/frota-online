using FrotaOnline.Application.Common;
using FrotaOnline.Application.Signatures.Dtos;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Signatures;

public interface ISignatureService
{
    Task<Result<SignatureResponse>> RegisterAsync(
        Guid userId, RegisterSignatureRequest request, CancellationToken cancellationToken);

    Task<Result<SignatureImage>> GetImageAsync(
        Guid userId, Guid freightId, SignatureType type, CancellationToken cancellationToken);
}
