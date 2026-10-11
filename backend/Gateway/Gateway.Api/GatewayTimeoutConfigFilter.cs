using Yarp.ReverseProxy.Configuration;

namespace Gateway.Api;

/// <summary>
/// Enforces a strict 3-minute timeout on all reverse-proxy routes
/// so YARP does not abort downstream requests or startup migrations at the default 100 seconds.
/// </summary>
public sealed class GatewayTimeoutConfigFilter : IProxyConfigFilter
{
    private static readonly TimeSpan StrictTimeout = TimeSpan.FromMinutes(3);

    public ValueTask<ClusterConfig> ConfigureClusterAsync(ClusterConfig cluster, CancellationToken cancel)
    {
        return ValueTask.FromResult(cluster);
    }

    public ValueTask<RouteConfig> ConfigureRouteAsync(RouteConfig route, ClusterConfig? cluster, CancellationToken cancel)
    {
        return ValueTask.FromResult(route with { Timeout = StrictTimeout });
    }
}
