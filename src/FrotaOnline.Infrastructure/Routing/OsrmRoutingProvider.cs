using System.Globalization;
using System.Text;
using System.Text.Json;
using FrotaOnline.Application.Abstractions.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FrotaOnline.Infrastructure.Routing;

/// <summary>OSRM "route" service (https://project-osrm.org). Free and open source.</summary>
internal sealed class OsrmRoutingProvider : IRoutingProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<OsrmRoutingProvider> _logger;

    public OsrmRoutingProvider(HttpClient http, IOptions<RoutingOptions> options, ILogger<OsrmRoutingProvider> logger)
    {
        _http = http;
        _logger = logger;
        _http.BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);
        // The public OSRM server rejects requests without an identifiable User-Agent.
        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("FrotaOnline/1.0 (+https://github.com/enzzo-dev/frota-online)");
        }
    }

    public async Task<RoutePath?> GetRouteAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken)
    {
        if (points.Count < 2)
        {
            return null;
        }

        try
        {
            // OSRM wants longitude first: /route/v1/driving/lon,lat;lon,lat
            var coordinates = string.Join(';', points.Select(p =>
                string.Create(CultureInfo.InvariantCulture, $"{p.Longitude:0.######},{p.Latitude:0.######}")));
            var url = $"route/v1/driving/{coordinates}?overview=simplified&geometries=geojson&steps=false";

            using var response = await _http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("OSRM answered {Status} for {Url}: {Body}. Distance and duration will be missing.",
                    (int)response.StatusCode, _http.BaseAddress + url, body.Length > 300 ? body[..300] : body);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var parsed = Parse(json.RootElement);
            if (parsed is null)
            {
                _logger.LogError("OSRM returned no route for {Url}: {Body}", _http.BaseAddress + url, json.RootElement.ToString());
            }

            return parsed;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            _logger.LogError(ex, "OSRM is unavailable ({Base}); distance and duration will be missing.", _http.BaseAddress);
            return null;
        }
    }

    internal static RoutePath? Parse(JsonElement root)
    {
        if (!root.TryGetProperty("code", out var code) || code.GetString() != "Ok"
            || !root.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0)
        {
            return null;
        }

        var route = routes[0];
        var path = new List<GeoPoint>();
        foreach (var position in route.GetProperty("geometry").GetProperty("coordinates").EnumerateArray())
        {
            // GeoJSON order: [longitude, latitude]
            path.Add(new GeoPoint(position[1].GetDouble(), position[0].GetDouble()));
        }

        return new RoutePath(
            (int)Math.Round(route.GetProperty("distance").GetDouble()),
            (int)Math.Round(route.GetProperty("duration").GetDouble()),
            path);
    }
}
