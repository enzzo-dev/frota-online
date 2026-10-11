using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Addresses;

public static class AddressErrors
{
    public static readonly Error InUse =
        new("address.in_use", "This address was already used in a freight, so it cannot be edited. Save a new address instead.", ErrorType.Conflict);

    public static readonly Error InActiveUse =
        new("address.in_active_use", "This address is in an open freight or a delivery in progress. Cancel it first.", ErrorType.Conflict);

    public static readonly Error NotFound =
        new("address.not_found", "Address not found.", ErrorType.NotFound);
}
