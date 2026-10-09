using FluentValidation;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Terms;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Terms.Dtos;
using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Terms;

public sealed class TermService : ITermService
{
    private readonly ITermRepository _terms;
    private readonly IConsentRepository _consents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<AcceptTermRequest> _acceptValidator;
    private readonly IValidator<PublishTermRequest> _publishValidator;

    public TermService(
        ITermRepository terms,
        IConsentRepository consents,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IValidator<AcceptTermRequest> acceptValidator,
        IValidator<PublishTermRequest> publishValidator)
    {
        _terms = terms;
        _consents = consents;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _acceptValidator = acceptValidator;
        _publishValidator = publishValidator;
    }

    public async Task<Result<IReadOnlyList<TermResponse>>> GetActiveAsync(CancellationToken cancellationToken)
    {
        var active = await _terms.ListActiveAsync(cancellationToken);
        return Result.Success<IReadOnlyList<TermResponse>>(active.Select(ToResponse).ToList());
    }

    public async Task<Result<IReadOnlyList<TermResponse>>> GetPendingAsync(Guid userId, CancellationToken cancellationToken)
    {
        var active = await _terms.ListActiveAsync(cancellationToken);
        var consents = await _consents.ListByUserAsync(userId, cancellationToken);
        var accepted = consents.Select(c => c.TermDocumentId).ToHashSet();

        return Result.Success<IReadOnlyList<TermResponse>>(
            active.Where(term => !accepted.Contains(term.Id)).Select(ToResponse).ToList());
    }

    public async Task<Result<IReadOnlyList<ConsentResponse>>> GetConsentsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var consents = await _consents.ListByUserAsync(userId, cancellationToken);
        var terms = await _terms.ListByIdsAsync(consents.Select(c => c.TermDocumentId).ToList(), cancellationToken);
        var termsById = terms.ToDictionary(t => t.Id);

        var response = consents
            .Where(c => termsById.ContainsKey(c.TermDocumentId))
            .OrderByDescending(c => c.AcceptedAt)
            .Select(c => ToResponse(c, termsById[c.TermDocumentId]))
            .ToList();

        return Result.Success<IReadOnlyList<ConsentResponse>>(response);
    }

    public async Task<Result<ConsentResponse>> AcceptAsync(
        Guid userId, AcceptTermRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var validation = await _acceptValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ConsentResponse>(ValidationMapper.ToError(validation));
        }

        var term = await _terms.GetByIdAsync(request.TermDocumentId, cancellationToken);
        if (term is null)
        {
            return Result.Failure<ConsentResponse>(TermErrors.NotFound);
        }

        // Accepting twice is harmless: return the original consent instead of failing.
        var consents = await _consents.ListByUserAsync(userId, cancellationToken);
        var existing = consents.FirstOrDefault(c => c.TermDocumentId == term.Id);
        if (existing is not null)
        {
            return Result.Success(ToResponse(existing, term));
        }

        if (!term.IsActive)
        {
            return Result.Failure<ConsentResponse>(TermErrors.NotActive);
        }

        var consent = UserConsent.Create(userId, term.Id, _clock.UtcNow, ipAddress);
        await _consents.AddAsync(consent, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ToResponse(consent, term));
    }

    public async Task<Result<TermResponse>> PublishAsync(PublishTermRequest request, CancellationToken cancellationToken)
    {
        var validation = await _publishValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<TermResponse>(ValidationMapper.ToError(validation));
        }

        var version = request.Version.Trim();
        if (await _terms.VersionExistsAsync(request.Type, version, cancellationToken))
        {
            return Result.Failure<TermResponse>(TermErrors.VersionAlreadyPublished);
        }

        var active = await _terms.ListActiveAsync(cancellationToken);
        foreach (var previous in active.Where(t => t.Type == request.Type))
        {
            previous.Deactivate();
        }

        var term = TermDocument.Create(request.Type, version, request.Title, request.ContentUrl, _clock.UtcNow);
        await _terms.AddAsync(term, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ToResponse(term));
    }

    private static TermResponse ToResponse(TermDocument term) =>
        new(term.Id, term.Type, term.Version, term.Title, term.ContentUrl, term.PublishedAt);

    private static ConsentResponse ToResponse(UserConsent consent, TermDocument term) =>
        new(term.Id, term.Type, term.Version, consent.AcceptedAt);
}
