using FrotaOnline.Application.Common;
using FrotaOnline.Application.Drivers.Dtos;

namespace FrotaOnline.Application.Drivers;

public interface IDriverDocumentService
{
    Task<Result<IReadOnlyList<MediaFileResponse>>> ListAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Records a file the app already uploaded to storage. The key must live under <c>drivers/{userId}/</c>.
    /// </summary>
    Task<Result<MediaFileResponse>> RegisterAsync(Guid userId, RegisterMediaRequest request, CancellationToken cancellationToken);
}
