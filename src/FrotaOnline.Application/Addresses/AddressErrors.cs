using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Addresses;

public static class AddressErrors
{
    public static readonly Error NotFound =
        new("address.not_found", "Address not found.", ErrorType.NotFound);
}
