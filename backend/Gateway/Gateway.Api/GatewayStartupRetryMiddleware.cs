using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace Gateway.Api;

/// <summary>
/// Intercepts transient downstream connection failures (such as services starting up or binding ports)
/// and retries the reverse-proxy forwarder with backoff for up to 45 seconds instead of immediately
/// returning 502 Bad Gateway to the caller.
/// </summary>
public static class GatewayStartupRetryMiddleware
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxTotalWaitTime = TimeSpan.FromSeconds(45);

    public static async Task ExecuteAsync(HttpContext context, Func<Task> next, ILogger logger)
    {
        DateTime startUtc = DateTime.UtcNow;
        int attempt = 0;
        TimeSpan currentDelay = InitialDelay;

        while (true)
        {
            attempt++;

            if (context.Request.Body.CanSeek)
            {
                context.Request.Body.Position = 0;
            }

            await next();

            IForwarderErrorFeature? errorFeature = context.GetForwarderErrorFeature();
            if (errorFeature is null || context.Response.HasStarted)
            {
                // Successful forwarding, or response has already started streaming to client.
                return;
            }

            bool isStartupOrTransient =
                errorFeature.Error == ForwarderError.Request ||
                errorFeature.Error == ForwarderError.NoAvailableDestinations ||
                errorFeature.Error == ForwarderError.RequestTimedOut;

            if (!isStartupOrTransient)
            {
                return;
            }

            TimeSpan elapsed = DateTime.UtcNow - startUtc;
            if (elapsed + currentDelay >= MaxTotalWaitTime || context.RequestAborted.IsCancellationRequested)
            {
                logger.LogWarning(
                    "Gateway downstream retry exhausted after {Attempt} attempts ({ElapsedMs}ms) for path {Path}: {Error}",
                    attempt, (int)elapsed.TotalMilliseconds, context.Request.Path, errorFeature.Error);
                return;
            }

            logger.LogInformation(
                "Downstream service not ready for {Method} {Path} ({Error}, attempt {Attempt}); waiting {DelayMs}ms to retry...",
                context.Request.Method, context.Request.Path, errorFeature.Error, attempt, (int)currentDelay.TotalMilliseconds);

            // Reset response status and feature before retrying
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.Headers.Clear();
            context.Features.Set<IForwarderErrorFeature>(null);

            // If ReverseProxyFeature was populated, ensure AvailableDestinations are preserved
            IReverseProxyFeature? proxyFeature = context.GetReverseProxyFeature();
            if (proxyFeature != null && proxyFeature.AvailableDestinations.Count == 0 && proxyFeature.AllDestinations.Count > 0)
            {
                proxyFeature.AvailableDestinations = proxyFeature.AllDestinations;
            }

            try
            {
                await Task.Delay(currentDelay, context.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // Exponential backoff up to MaxDelay
            currentDelay = TimeSpan.FromMilliseconds(Math.Min(currentDelay.TotalMilliseconds * 1.5, MaxDelay.TotalMilliseconds));
        }
    }
}
