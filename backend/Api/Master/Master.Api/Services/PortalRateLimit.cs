using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Master.Api.Services;

/// <summary>
/// The limit on exchanging portal codes (TK-94): ten tries a minute per
/// address. A code's secret is 256 bits, so this is not what stops a guess
/// from landing; it stops the route being used to hammer the database.
/// </summary>
public static class PortalRateLimit
{
    public const string Policy = "portal-session";

    public const int PermitsPerMinute = 10;

    public static IServiceCollection AddPortalRateLimit(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Policy, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientAddress(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = PermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

    /// <summary>
    /// The caller's address. Behind the gateway the connection is the
    /// gateway's, so the last <c>X-Forwarded-For</c> entry — the one the
    /// gateway appended, which a client cannot forge — is the address it saw.
    /// </summary>
    public static string ClientAddress(HttpContext context)
    {
        string? forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            string last = forwarded.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)[^1];
            if (IPAddress.TryParse(last, out IPAddress? address))
            {
                return address.ToString();
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
