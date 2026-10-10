using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Deliveries.Dtos;
using FrotaOnline.Application.Marketplace;

namespace FrotaOnline.Application.Deliveries;

public sealed class CustomerDeliveryService : ICustomerDeliveryService
{
    private readonly IDeliveryQueries _queries;

    public CustomerDeliveryService(IDeliveryQueries queries) => _queries = queries;

    public async Task<Result<IReadOnlyList<CustomerDeliverySummary>>> ListAsync(
        Guid customerId, CancellationToken cancellationToken) =>
        Result.Success(await _queries.ListByCustomerAsync(customerId, cancellationToken));

    public async Task<Result<CustomerDeliveryDetail>> GetAsync(
        Guid customerId, Guid deliveryId, CancellationToken cancellationToken)
    {
        var detail = await _queries.GetForCustomerAsync(customerId, deliveryId, cancellationToken);
        return detail is null
            ? Result.Failure<CustomerDeliveryDetail>(MarketplaceErrors.DeliveryNotFound)
            : Result.Success(detail);
    }
}
