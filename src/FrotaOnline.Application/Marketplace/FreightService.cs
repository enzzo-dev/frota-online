using FluentValidation;
using FrotaOnline.Application.Abstractions.Addresses;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Persistence;
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
    private readonly ITermAcceptanceChecker _terms;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<CreateFreightRequest> _validator;

    public FreightService(
        IFreightRepository freights,
        IProposalRepository proposals,
        IAddressRepository addresses,
        ITermAcceptanceChecker terms,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IValidator<CreateFreightRequest> validator)
    {
        _freights = freights;
        _proposals = proposals;
        _addresses = addresses;
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

        var pickup = await _addresses.GetByIdAsync(request.PickupAddressId, cancellationToken);
        var delivery = await _addresses.GetByIdAsync(request.DeliveryAddressId, cancellationToken);
        if (pickup is null || pickup.OwnerUserId != customerId || delivery is null || delivery.OwnerUserId != customerId)
        {
            return Result.Failure<FreightResponse>(AddressErrors.NotFound);
        }

        var freight = FreightRequest.Create(
            customerId, pickup.Id, delivery.Id, request.CargoDescription, request.CargoWeightKg,
            request.RequiredVehicleType, request.ScheduledPickupAt?.ToUniversalTime(), now);

        await _freights.AddAsync(freight, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var addresses = new Dictionary<Guid, Address> { [pickup.Id] = pickup, [delivery.Id] = delivery };
        return Result.Success(FreightMapper.ToResponse(freight, addresses, 0));
    }

    public async Task<Result<IReadOnlyList<FreightResponse>>> ListAsync(
        Guid customerId, FreightStatus? status, CancellationToken cancellationToken)
    {
        var freights = await _freights.ListByCustomerAsync(customerId, status, cancellationToken);
        var responses = await BuildResponsesAsync(freights, cancellationToken);
        return Result.Success(responses);
    }

    public async Task<Result<FreightResponse>> GetAsync(Guid customerId, Guid freightId, CancellationToken cancellationToken)
    {
        var freight = await _freights.GetByIdAsync(freightId, cancellationToken);
        if (freight is null || freight.CustomerId != customerId)
        {
            return Result.Failure<FreightResponse>(MarketplaceErrors.FreightNotFound);
        }

        var responses = await BuildResponsesAsync(new[] { freight }, cancellationToken);
        return Result.Success(responses[0]);
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

    private async Task<IReadOnlyList<FreightResponse>> BuildResponsesAsync(
        IReadOnlyCollection<FreightRequest> freights, CancellationToken cancellationToken)
    {
        if (freights.Count == 0)
        {
            return Array.Empty<FreightResponse>();
        }

        var addressIds = freights.SelectMany(f => new[] { f.PickupAddressId, f.DeliveryAddressId }).Distinct().ToList();
        var addresses = (await _addresses.ListByIdsAsync(addressIds, cancellationToken)).ToDictionary(a => a.Id);
        var counts = await _proposals.CountPendingByFreightAsync(freights.Select(f => f.Id).ToList(), cancellationToken);

        return freights
            .Select(f => FreightMapper.ToResponse(f, addresses, counts.TryGetValue(f.Id, out var count) ? count : 0))
            .ToList();
    }
}
