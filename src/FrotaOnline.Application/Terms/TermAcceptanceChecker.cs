using FrotaOnline.Application.Abstractions.Terms;

namespace FrotaOnline.Application.Terms;

public sealed class TermAcceptanceChecker : ITermAcceptanceChecker
{
    private readonly ITermRepository _terms;
    private readonly IConsentRepository _consents;

    public TermAcceptanceChecker(ITermRepository terms, IConsentRepository consents)
    {
        _terms = terms;
        _consents = consents;
    }

    public async Task<bool> HasAcceptedAllActiveAsync(Guid userId, CancellationToken cancellationToken)
    {
        var active = await _terms.ListActiveAsync(cancellationToken);
        var consents = await _consents.ListByUserAsync(userId, cancellationToken);
        var accepted = consents.Select(c => c.TermDocumentId).ToHashSet();
        return active.All(term => accepted.Contains(term.Id));
    }
}
