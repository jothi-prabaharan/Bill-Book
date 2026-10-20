using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Master.Api.Services;

/// <summary>
/// The limit on the anonymous routes that send or check a one-time code
/// (TK-196): ten requests a minute per address, across signup, forgot-password,
/// verify-otp and reset-password.
///
/// Each number already waits a minute between codes and gets five an hour
/// (<c>OtpPolicy</c>), and each code allows five tries. What those cannot stop
/// is one address walking through many numbers, each WhatsApp or SMS message
/// paid for by the platform; this does. A refusal is a bare 429, which the
/// pages read as "too many tries from this connection".
/// </summary>
public static class OtpRateLimit
{
    public const string Policy = "otp";

    public const int PermitsPerMinute = 10;

    public static IServiceCollection AddOtpRateLimit(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Policy, context => RateLimitPartition.GetFixedWindowLimiter(
                PortalRateLimit.ClientAddress(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = PermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });
}
