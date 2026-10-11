using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace Gateway.Api;

/// <summary>
/// Enforces a strict 3-minute timeout on all reverse-proxy routes and clusters
/// so YARP does not abort downstream requests or startup migrations at the default 100 seconds.
/// </summary>
public sealed class GatewayTimeoutConfigFilter : IProxyConfigFilter
{
    private static readonly TimeSpan StrictTimeout = TimeSpan.FromMinutes(3);

    public ValueTask<ClusterConfig> ConfigureClusterAsync(ClusterConfig cluster, CancellationToken cancel)
    {
        ForwarderRequestConfig requestConfig = (cluster.HttpRequest ?? new ForwarderRequestConfig()) with
        {
            ActivityTimeout = StrictTimeout
        };
        return ValueTask.FromResult(cluster with { HttpRequest = requestConfig });
    }

    public ValueTask<RouteConfig> ConfigureRouteAsync(RouteConfig route, ClusterConfig? cluster, CancellationToken cancel)
    {
        return ValueTask.FromResult(route with { Timeout = StrictTimeout });
    }
}
