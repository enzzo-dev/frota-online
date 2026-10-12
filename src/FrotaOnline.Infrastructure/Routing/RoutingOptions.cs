using System.ComponentModel.DataAnnotations;

namespace FrotaOnline.Infrastructure.Routing;

public sealed class RoutingOptions
{
    public const string SectionName = "Routing";

    /// <summary>
    /// OSRM server. The public demo server is for testing only and has no uptime promise:
    /// run your own OSRM (Docker image osrm/osrm-backend with the Brazil extract) before going to production.
    /// </summary>
    [Required, Url]
    public string BaseUrl { get; init; } = "https://router.project-osrm.org";

    /// <summary>Longer than this and the request goes on without the route line.</summary>
    [Range(1, 30)]
    public int TimeoutSeconds { get; init; } = 10;
}
