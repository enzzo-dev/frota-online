using Microsoft.Extensions.Caching.Memory;

namespace FrotaOnline.Infrastructure.Routing;

internal sealed class RouteCache : IDisposable
{
    public RouteCache() => Entries = new MemoryCache(new MemoryCacheOptions { SizeLimit = 2_000 });

    public MemoryCache Entries { get; }

    public void Dispose() => Entries.Dispose();
}
