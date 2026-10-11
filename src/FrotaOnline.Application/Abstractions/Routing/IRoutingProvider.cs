namespace FrotaOnline.Application.Abstractions.Routing;

public readonly record struct GeoPoint(double Latitude, double Longitude);

/// <summary>A drivable route through the points, in the order given.</summary>
public sealed record RoutePath(int DistanceMeters, int DurationSeconds, IReadOnlyList<GeoPoint> Points);

/// <summary>
/// Calculates road routes. The application only knows this interface, so the engine (OSRM today) can be swapped
/// for Google, Mapbox or another one by writing one class and changing one DI line.
/// </summary>
public interface IRoutingProvider
{
    /// <summary>
    /// Null when the engine is down, too slow, or finds no road between the points. Callers must cope with that:
    /// a map without a line is better than a failed request.
    /// </summary>
    Task<RoutePath?> GetRouteAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken);
}
