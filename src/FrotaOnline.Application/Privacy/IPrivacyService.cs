using FrotaOnline.Application.Common;
using FrotaOnline.Application.Privacy.Dtos;

namespace FrotaOnline.Application.Privacy;

public interface IPrivacyService
{
    /// <summary>Right of access and portability (LGPD art. 18, II and V).</summary>
    Task<Result<PersonalDataExport>> ExportAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Right to elimination of personal data (LGPD art. 18, VI), keeping only what the law requires.</summary>
    Task<Result<AccountDeletionResponse>> DeleteAccountAsync(
        Guid userId, DeleteAccountRequest request, CancellationToken cancellationToken);
}
