namespace FrotaOnline.Application.Marketplace;

public interface IFreightExpirationService
{
    /// <summary>
    /// Expires open freights and pending proposals whose window ended.
    /// Returns how many freights and proposals changed.
    /// </summary>
    Task<(int Freights, int Proposals)> ExpireDueAsync(CancellationToken cancellationToken);
}
