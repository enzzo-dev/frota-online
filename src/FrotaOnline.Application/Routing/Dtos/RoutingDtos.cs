using FrotaOnline.Application.Abstractions.Routing;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Routing.Dtos;

/// <summary>Saved address ids in the order of the run. At least two, each with coordinates.</summary>
public sealed record RoutePreviewRequest(IReadOnlyList<Guid> AddressIds);

/// <summary>A pin on the map. StopType is null in a preview, where the customer has not set the types yet.</summary>
public sealed record RouteWaypoint(int Sequence, StopType? Type, string Label, double Latitude, double Longitude);

/// <summary>
/// Everything a map screen needs. When the routing engine is unavailable, Path is empty and the distance and
/// duration are null: draw the pins and a straight line instead.
/// </summary>
public sealed record RouteMapResponse(
    int? DistanceMeters,
    int? DurationSeconds,
    IReadOnlyList<RouteWaypoint> Waypoints,
    IReadOnlyList<GeoPoint> Path);
