namespace FrotaOnline.Application.Abstractions.Marketplace;

public interface IFreightViewRepository
{
    Task RecordAsync(Guid freightRequestId, Guid driverProfileId, DateTime utcNow, CancellationToken cancellationToken);
}
