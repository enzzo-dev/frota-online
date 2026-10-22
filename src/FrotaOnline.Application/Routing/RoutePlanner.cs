using FrotaOnline.Application.Abstractions.Routing;
using FrotaOnline.Application.Routing.Dtos;
using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Routing;

public interface IRoutePlanner
{
    /// <summary>
    /// Builds the map for the stops in order. Returns null if any address has no coordinates;
    /// the caller decides whether that is an error.
    /// </summary>
    Task<RouteMapResponse?> PlanAsync(IReadOnlyList<RoutedStop> stops, CancellationToken cancellationToken);

    RouteMapResponse? Restore(IReadOnlyList<RoutedStop> stops, int distanceMeters, int durationSeconds, string polyline);
}

/// <summary>An address on the run, paired with the stop type when there is one.</summary>
public sealed record RoutedStop(Address Address, Domain.Enums.StopType? Type);

public sealed class RoutePlanner : IRoutePlanner
{
    private readonly IRoutingProvider _provider;

    public RoutePlanner(IRoutingProvider provider) => _provider = provider;

    public async Task<RouteMapResponse?> PlanAsync(IReadOnlyList<RoutedStop> stops, CancellationToken cancellationToken)
    {
        if (!HaveCoordinates(stops))
        {
            return null;
        }

        var waypoints = ToWaypoints(stops);
        var route = await _provider.GetRouteAsync(
            waypoints.Select(w => new GeoPoint(w.Latitude, w.Longitude)).ToList(), cancellationToken);

        return route is null
            ? new RouteMapResponse(null, null, waypoints, Array.Empty<GeoPoint>())
            : new RouteMapResponse(route.DistanceMeters, route.DurationSeconds, waypoints, route.Points);
    }

    public RouteMapResponse? Restore(IReadOnlyList<RoutedStop> stops, int distanceMeters, int durationSeconds, string polyline) =>
        HaveCoordinates(stops)
            ? new RouteMapResponse(distanceMeters, durationSeconds, ToWaypoints(stops), PolylineCodec.Decode(polyline))
            : null;

    private static bool HaveCoordinates(IReadOnlyList<RoutedStop> stops) =>
        stops.All(s => s.Address.Latitude is not null && s.Address.Longitude is not null);

    private static List<RouteWaypoint> ToWaypoints(IReadOnlyList<RoutedStop> stops) =>
        stops
            .Select((s, i) => new RouteWaypoint(i + 1, s.Type, s.Address.Label, s.Address.Latitude!.Value, s.Address.Longitude!.Value))
            .ToList();
}
