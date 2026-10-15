using FluentValidation;
using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Terms;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Drivers.Dtos;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Drivers;

public sealed class DriverProfileService : IDriverProfileService
{
    private readonly IDriverRepository _drivers;
    private readonly IVehicleRepository _vehicles;
    private readonly IMediaFileRepository _media;
    private readonly ITermRepository _terms;
    private readonly IConsentRepository _consents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<UpsertDriverProfileRequest> _validator;

    public DriverProfileService(
        IDriverRepository drivers,
        IVehicleRepository vehicles,
        IMediaFileRepository media,
        ITermRepository terms,
        IConsentRepository consents,
        IUnitOfWork unitOfWork,
        IValidator<UpsertDriverProfileRequest> validator)
    {
        _drivers = drivers;
        _vehicles = vehicles;
        _media = media;
        _terms = terms;
        _consents = consents;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<Result<DriverProfileResponse>> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<DriverProfileResponse>(DriverErrors.ProfileNotFound);
        }

        return Result.Success(await ToResponseAsync(profile, cancellationToken));
    }

    public async Task<Result<DriverProfileResponse>> UpsertAsync(
        Guid userId, UpsertDriverProfileRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<DriverProfileResponse>(ValidationMapper.ToError(validation));
        }

        var taxId = BrazilianDocuments.NormalizeTaxId(request.TaxId);
        var cnh = BrazilianDocuments.OnlyDigits(request.CnhNumber);

        if (await _drivers.TaxIdInUseAsync(taxId, userId, cancellationToken))
        {
            return Result.Failure<DriverProfileResponse>(DriverErrors.TaxIdInUse);
        }

        if (await _drivers.CnhInUseAsync(cnh, userId, cancellationToken))
        {
            return Result.Failure<DriverProfileResponse>(DriverErrors.CnhInUse);
        }

        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            profile = DriverProfile.Create(userId, taxId, cnh, request.CnhCategory);
            await _drivers.AddAsync(profile, cancellationToken);
        }
        else if (profile.IsEditable)
        {
            profile.UpdateDetails(taxId, cnh, request.CnhCategory);
        }
        else
        {
            return Result.Failure<DriverProfileResponse>(DriverErrors.ProfileLocked);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await ToResponseAsync(profile, cancellationToken));
    }

    public async Task<Result<DriverProfileResponse>> SubmitForReviewAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<DriverProfileResponse>(DriverErrors.ProfileNotFound);
        }

        if (!profile.IsEditable)
        {
            return Result.Failure<DriverProfileResponse>(DriverErrors.InvalidStatusForSubmit);
        }

        var checklist = await BuildChecklistAsync(profile, cancellationToken);
        if (!checklist.IsReady)
        {
            return Result.Failure<DriverProfileResponse>(DriverErrors.SubmissionIncomplete(DescribeMissing(checklist)));
        }

        profile.SubmitForReview();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await ToResponseAsync(profile, cancellationToken));
    }

    private async Task<SubmissionChecklist> BuildChecklistAsync(DriverProfile profile, CancellationToken cancellationToken)
    {
        var activeTerms = await _terms.ListActiveAsync(cancellationToken);
        var consents = await _consents.ListByUserAsync(profile.UserId, cancellationToken);
        var accepted = consents.Select(c => c.TermDocumentId).ToHashSet();
        var termsAccepted = activeTerms.All(term => accepted.Contains(term.Id));

        var vehicles = await _vehicles.ListActiveByDriverAsync(profile.Id, cancellationToken);
        var documents = await _media.ListByOwnerAsync(profile.UserId, new[] { MediaKind.DriverDocument }, cancellationToken);

        return new SubmissionChecklist(termsAccepted, vehicles.Count > 0, documents.Count > 0);
    }

    private async Task<DriverProfileResponse> ToResponseAsync(DriverProfile profile, CancellationToken cancellationToken)
    {
        var checklist = await BuildChecklistAsync(profile, cancellationToken);
        return new DriverProfileResponse(
            profile.Id, profile.TaxId, profile.CnhNumber, profile.CnhCategory,
            profile.Status, profile.RejectionReason, profile.ReviewedAt, checklist);
    }

    private static Dictionary<string, string[]> DescribeMissing(SubmissionChecklist checklist)
    {
        var missing = new Dictionary<string, string[]>();
        if (!checklist.TermsAccepted)
        {
            missing["terms"] = new[] { "Aceite todos os termos ativos (GET /api/v1/terms/pending)." };
        }

        if (!checklist.HasActiveVehicle)
        {
            missing["vehicles"] = new[] { "Cadastre ao menos um veículo." };
        }

        if (!checklist.HasDocument)
        {
            missing["documents"] = new[] { "Envie ao menos um documento do motorista (CNH, por exemplo)." };
        }

        return missing;
    }
}
