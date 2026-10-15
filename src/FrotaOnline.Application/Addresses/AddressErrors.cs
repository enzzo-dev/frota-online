using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Addresses;

public static class AddressErrors
{
    public static readonly Error InUse =
        new("address.in_use", "Este endereço já foi usado em um frete e não pode ser editado. Salve um novo endereço.", ErrorType.Conflict);

    public static readonly Error InActiveUse =
        new("address.in_active_use", "Este endereço está em um frete aberto ou em uma entrega em andamento. Cancele-o primeiro.", ErrorType.Conflict);

    public static readonly Error NotFound =
        new("address.not_found", "Endereço não encontrado.", ErrorType.NotFound);
}
