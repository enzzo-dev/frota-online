using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Time;

namespace FrotaOnline.Application.Marketplace;

public sealed class FreightExpirationService : IFreightExpirationService
{
    private readonly IFreightRepository _freights;
    private readonly IProposalRepository _proposals;
    private readonly IDateTimeProvider _clock;

    public FreightExpirationService(IFreightRepository freights, IProposalRepository proposals, IDateTimeProvider clock)
    {
        _freights = freights;
        _proposals = proposals;
        _clock = clock;
    }

    /// <summary>Two set-based UPDATE statements; nothing is loaded into the application.</summary>
    public async Task<(int Freights, int Proposals)> ExpireDueAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var freights = await _freights.ExpireDueAsync(now, cancellationToken);
        var proposals = await _proposals.ExpireDueAsync(now, cancellationToken);
        return (freights, proposals);
    }
}
