using FrotaOnline.Application.Admin.Dtos;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Drivers.Dtos;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Admin;

public interface IDriverReviewService
{
    Task<Result<IReadOnlyList<DriverReviewSummary>>> ListAsync(DriverStatus status, CancellationToken cancellationToken);

    Task<Result<DriverReviewDetail>> GetAsync(Guid driverId, CancellationToken cancellationToken);

    /// <summary>A short-lived link (10 minutes) to open one of the driver's files during review.</summary>
    Task<Result<DownloadUrlResponse>> GetFileUrlAsync(Guid driverId, Guid fileId, CancellationToken cancellationToken);

    Task<Result> ApproveAsync(Guid reviewerId, Guid driverId, CancellationToken cancellationToken);

    Task<Result> RejectAsync(Guid reviewerId, Guid driverId, RejectDriverRequest request, CancellationToken cancellationToken);
}
