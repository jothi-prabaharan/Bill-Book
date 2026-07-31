using System.Diagnostics;
using System.Security.Claims;
using Gateway.Entity.TableEntities;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Model;

namespace Gateway.Logging;

/// <summary>
/// Captures one <see cref="RequestLog"/> per proxied request and hands it to the
/// background writer. Everything expensive happens off the request path: this
/// middleware only reads values already in memory and enqueues.
/// </summary>
public class RequestLoggingMiddleware(RequestDelegate next, IRequestLogQueue queue, IOptions<RequestLogOptions> options)
{
    private readonly RequestLogOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled || IsExcluded(context.Request.Path))
        {
            await next(context);
            return;
        }

        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        long startTicks = Stopwatch.GetTimestamp();
        string? error = null;

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            // Record what failed, then rethrow: this middleware observes, it does
            // not handle. Type and message only, never a stack trace.
            error = Truncate($"{ex.GetType().Name}: {ex.Message}", 500);
            throw;
        }
        finally
        {
            // YARP populates the proxy feature during routing, so it is only
            // available after next() has run.
            IReverseProxyFeature? proxy = context.Features.Get<IReverseProxyFeature>();

            RequestLog entry = new()
            {
                StartedAt = startedAt,
                CorrelationId = context.TraceIdentifier,
                Method = context.Request.Method,
                Path = Truncate(context.Request.Path.Value ?? "/", 2048)!,
                QueryString = context.Request.QueryString.HasValue
                    ? Truncate(context.Request.QueryString.Value!.TrimStart('?'), 2048)
                    : null,
                StatusCode = context.Response.StatusCode,
                DurationMs = (int)Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds,
                ClusterId = Truncate(proxy?.Route?.Config?.ClusterId, 64),
                RouteId = Truncate(proxy?.Route?.Config?.RouteId, 64),
                Destination = Truncate(proxy?.ProxiedDestination?.Model?.Config?.Address, 300),
                CustomerId = ClaimAsGuid(context.User, "customer_id"),
                OrgId = ClaimAsGuid(context.User, "org_id"),
                UserId = ClaimAsGuid(context.User, ClaimTypes.NameIdentifier) ?? ClaimAsGuid(context.User, "sub"),
                ClientIp = Truncate(context.Connection.RemoteIpAddress?.ToString(), 45),
                UserAgent = Truncate(context.Request.Headers.UserAgent.ToString(), 300),
                RequestBytes = context.Request.ContentLength ?? 0,
                ResponseBytes = context.Response.ContentLength ?? 0,
                Error = error,
            };

            queue.TryWrite(entry);
        }
    }

    private bool IsExcluded(PathString path) =>
        _options.ExcludedPaths.Any(excluded =>
            path.Equals(excluded, StringComparison.OrdinalIgnoreCase) ||
            (excluded.Length > 1 && path.StartsWithSegments(excluded, StringComparison.OrdinalIgnoreCase)));

    private static Guid? ClaimAsGuid(ClaimsPrincipal? user, string claimType) =>
        Guid.TryParse(user?.FindFirst(claimType)?.Value, out Guid value) ? value : null;

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) ? value
        : value.Length <= maxLength ? value
        : value[..maxLength];
}
