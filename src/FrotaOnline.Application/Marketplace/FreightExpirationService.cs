using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Time;

namespace FrotaOnline.Application.Marketplace;

public sealed class FreightExpirationService : IFreightExpirationService
{
    private readonly IFreightRepository _freights;
    private readonly IProposalRepository _proposals;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public FreightExpirationService(
        IFreightRepository freights, IProposalRepository proposals, IUnitOfWork unitOfWork, IDateTimeProvider clock)
    {
        _freights = freights;
        _proposals = proposals;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<(int Freights, int Proposals)> ExpireDueAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        var freights = 0;
        foreach (var freight in await _freights.ListOpenDueAsync(now, cancellationToken))
        {
            if (freight.ExpireIfDue(now))
            {
                freights++;
            }
        }

        var proposals = 0;
        foreach (var proposal in await _proposals.ListPendingDueAsync(now, cancellationToken))
        {
            if (proposal.ExpireIfDue(now))
            {
                proposals++;
            }
        }

        if (freights + proposals > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return (freights, proposals);
    }
}
