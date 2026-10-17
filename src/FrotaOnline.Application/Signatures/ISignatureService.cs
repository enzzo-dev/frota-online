using FrotaOnline.Application.Common;
using FrotaOnline.Application.Signatures.Dtos;

namespace FrotaOnline.Application.Signatures;

public interface ISignatureService
{
    Task<Result<SignatureUploadUrlResponse>> CreateUploadUrlAsync(
        Guid userId, CreateSignatureUploadUrlRequest request, CancellationToken cancellationToken);

    Task<Result<SignatureResponse>> RegisterAsync(
        Guid userId, RegisterSignatureRequest request, CancellationToken cancellationToken);
}
