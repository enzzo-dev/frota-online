namespace FrotaOnline.Application.Abstractions.Privacy;

public interface IAccountErasureStore
{
    /// <summary>True when the user is the customer or the driver of a delivery that is neither delivered nor cancelled.</summary>
    Task<bool> HasActiveDeliveriesAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Ids, at the payment provider, of the recurring charges still running for the driver, so they can be stopped before the erasure.</summary>
    Task<IReadOnlyList<string>> ListOpenSubscriptionIdsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Anonymizes the account and removes what has no legal reason to stay, all in one database transaction:
    /// open freights are cancelled, pending proposals are closed, files, device tokens and sessions are deleted.
    /// Freight, delivery and financial history stays, now pointing to an anonymous user.
    /// </summary>
    /// <returns>The storage keys of the files that were deleted from the database, so the caller can delete the objects too.</returns>
    Task<IReadOnlyList<string>> EraseAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken);
}
