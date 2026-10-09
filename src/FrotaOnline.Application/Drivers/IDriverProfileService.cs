using FrotaOnline.Application.Common;
using FrotaOnline.Application.Drivers.Dtos;

namespace FrotaOnline.Application.Drivers;

public interface IDriverProfileService
{
    Task<Result<DriverProfileResponse>> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Creates the profile or updates it while it is still editable (draft or rejected).</summary>
    Task<Result<DriverProfileResponse>> UpsertAsync(Guid userId, UpsertDriverProfileRequest request, CancellationToken cancellationToken);

    /// <summary>Moves the profile to PendingReview once terms, vehicle and documents are in place.</summary>
    Task<Result<DriverProfileResponse>> SubmitForReviewAsync(Guid userId, CancellationToken cancellationToken);
}
