namespace FrotaOnline.Application.Abstractions.Queries;

/// <summary>How freights use an address. Decides whether it can be edited or deleted.</summary>
public enum AddressUsage
{
    /// <summary>No freight ever used it.</summary>
    None = 0,

    /// <summary>Only finished freights (expired, cancelled, delivered) use it: history that must stay readable.</summary>
    History = 1,

    /// <summary>An open freight, or an approved one whose delivery is not finished yet, uses it.</summary>
    Active = 2
}

public interface IAddressUsageQueries
{
    Task<AddressUsage> GetUsageAsync(Guid addressId, CancellationToken cancellationToken);
}
