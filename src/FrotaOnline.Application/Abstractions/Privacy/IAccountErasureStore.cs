namespace FrotaOnline.Application.Abstractions.Privacy;

public interface IAccountErasureStore
{
    /// <summary>True when the user is the customer or the driver of a delivery that is neither delivered nor cancelled.</summary>
    Task<bool> HasActiveDeliveriesAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Anonymizes the account and removes what has no legal reason to stay, all in one database transaction:
    /// open freights are cancelled, pending proposals are closed, files, device tokens and sessions are deleted.
    /// Freight, delivery and financial history stays, now pointing to an anonymous user.
    /// </summary>
    Task EraseAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken);
}
