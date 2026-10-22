using System.Globalization;
using FrotaOnline.Application.Abstractions.Routing;
using Microsoft.Extensions.Caching.Memory;

namespace FrotaOnline.Infrastructure.Routing;

internal sealed class CachedRoutingProvider : IRoutingProvider
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    private readonly IRoutingProvider _inner;
    private readonly RouteCache _cache;

    public CachedRoutingProvider(IRoutingProvider inner, RouteCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<RoutePath?> GetRouteAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken)
    {
        var key = KeyOf(points);
        if (_cache.Entries.TryGetValue(key, out RoutePath? cached))
        {
            return cached;
        }

        var route = await _inner.GetRouteAsync(points, cancellationToken);
        if (route is not null)
        {
            _cache.Entries.Set(key, route, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = Lifetime });
        }

        return route;
    }

    private static string KeyOf(IReadOnlyList<GeoPoint> points) =>
        string.Join(';', points.Select(p => string.Create(CultureInfo.InvariantCulture, $"{p.Latitude:0.######},{p.Longitude:0.######}")));
}
