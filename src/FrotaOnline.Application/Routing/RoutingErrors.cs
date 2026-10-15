using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Routing;

public static class RoutingErrors
{
    public static Error MissingCoordinates(IEnumerable<string> labels) =>
        new("route.missing_coordinates",
            $"Estes endereços ainda não têm posição no mapa: {string.Join(", ", labels)}. Escolha-os no mapa e salve novamente.",
            ErrorType.Validation);
}
