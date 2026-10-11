using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Routing;

public static class RoutingErrors
{
    public static Error MissingCoordinates(IEnumerable<string> labels) =>
        new("route.missing_coordinates",
            $"These addresses have no map position yet: {string.Join(", ", labels)}. Pick them on the map and save again.",
            ErrorType.Validation);
}
