using FluentValidation;
using FrotaOnline.Application.Abstractions.Addresses;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Addresses;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Application.Terms;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Marketplace;

public sealed class FreightService : IFreightService
{
    private readonly IFreightRepository _freights;
    private readonly IProposalRepository _proposals;
    private readonly IAddressRepository _addresses;
    private readonly IFreightQueries _queries;
    private readonly ITermAcceptanceChecker _terms;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<CreateFreightRequest> _validator;

    public FreightService(
        IFreightRepository freights,
        IProposalRepository proposals,
        IAddressRepository addresses,
        IFreightQueries queries,
        ITermAcceptanceChecker terms,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IValidator<CreateFreightRequest> validator)
    {
        _freights = freights;
        _proposals = proposals;
        _addresses = addresses;
        _queries = queries;
        _terms = terms;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _validator = validator;
    }

    public async Task<Result<FreightResponse>> CreateAsync(
        Guid customerId, CreateFreightRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<FreightResponse>(ValidationMapper.ToError(validation));
        }

        var now = _clock.UtcNow;
        if (request.ScheduledPickupAt is not null && request.ScheduledPickupAt.Value.ToUniversalTime() <= now)
        {
            return Result.Failure<FreightResponse>(MarketplaceErrors.ScheduledInPast);
        }

        if (!await _terms.HasAcceptedAllActiveAsync(customerId, cancellationToken))
        {
            return Result.Failure<FreightResponse>(MarketplaceErrors.TermsNotAccepted);
        }

        var addressIds = request.Stops.Select(s => s.AddressId).Distinct().ToList();
        var found = (await _addresses.ListByIdsAsync(addressIds, cancellationToken)).ToDictionary(a => a.Id);
        if (addressIds.Any(id => !found.TryGetValue(id, out var address) || address.OwnerUserId != customerId))
        {
            return Result.Failure<FreightResponse>(AddressErrors.NotFound);
        }

        var drafts = request.Stops
            .Select(s => new FreightStopDraft(
                s.Type,
                s.AddressId,
                s.Note,
                (s.Items ?? Array.Empty<CreateFreightItemRequest>())
                    .Select(i => new FreightItemDraft(i.Description, i.Quantity))
                    .ToList()))
            .ToList();

        var freight = FreightRequest.Create(
            customerId, drafts, request.CargoDescription, request.CargoWeightKg,
            request.RequiredVehicleType, request.ScheduledPickupAt?.ToUniversalTime(), now);

        await _freights.AddAsync(freight, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FreightMapper.ToResponse(freight, found, 0));
    }

    public async Task<Result<IReadOnlyList<FreightResponse>>> ListAsync(
        Guid customerId, FreightStatus? status, CancellationToken cancellationToken)
    {
        var responses = await _queries.ListByCustomerAsync(customerId, status, cancellationToken);
        return Result.Success(responses);
    }

    public async Task<Result<FreightResponse>> GetAsync(Guid customerId, Guid freightId, CancellationToken cancellationToken)
    {
        var response = await _queries.GetForCustomerAsync(customerId, freightId, cancellationToken);
        return response is null
            ? Result.Failure<FreightResponse>(MarketplaceErrors.FreightNotFound)
            : Result.Success(response);
    }

    public async Task<Result> CancelAsync(Guid customerId, Guid freightId, CancellationToken cancellationToken)
    {
        var freight = await _freights.GetByIdAsync(freightId, cancellationToken);
        if (freight is null || freight.CustomerId != customerId)
        {
            return Result.Failure(MarketplaceErrors.FreightNotFound);
        }

        if (freight.Status != FreightStatus.Open)
        {
            return Result.Failure(MarketplaceErrors.FreightNotOpen);
        }

        freight.Cancel();
        foreach (var proposal in await _proposals.ListPendingByFreightAsync(freight.Id, cancellationToken))
        {
            proposal.Reject();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
