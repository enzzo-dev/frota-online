namespace FrotaOnline.Application.Terms;

public interface ITermAcceptanceChecker
{
    /// <summary>True when the user accepted the current version of every active term.</summary>
    Task<bool> HasAcceptedAllActiveAsync(Guid userId, CancellationToken cancellationToken);
}
