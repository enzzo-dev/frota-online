using FrotaOnline.Application.Common;
using FrotaOnline.Application.Terms.Dtos;

namespace FrotaOnline.Application.Terms;

public interface ITermService
{
    /// <summary>Current version of every term. Public: shown before sign-up.</summary>
    Task<Result<IReadOnlyList<TermResponse>>> GetActiveAsync(CancellationToken cancellationToken);

    /// <summary>Active terms the user has not accepted yet.</summary>
    Task<Result<IReadOnlyList<TermResponse>>> GetPendingAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<ConsentResponse>>> GetConsentsAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<ConsentResponse>> AcceptAsync(Guid userId, AcceptTermRequest request, string? ipAddress, CancellationToken cancellationToken);

    /// <summary>Publishes a new version and deactivates the previous active version of the same type.</summary>
    Task<Result<TermResponse>> PublishAsync(PublishTermRequest request, CancellationToken cancellationToken);
}
