using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Kernel.Internal;

/// <summary>
/// Guards a service-to-service endpoint with a shared key.
///
/// These endpoints take the organization to act on as a parameter rather than
/// from a JWT, because the caller is a background worker with no user token.
/// That is what makes them dangerous: without a guard, anything that can reach
/// the port could seed, or reseed, any organization it can name. They are also
/// deliberately not routed through the gateway, but "not routed" is a
/// configuration detail, not a control.
///
/// The key lives at <c>Internal:ApiKey</c> and is required — a blank one fails
/// every request rather than waving them through, so a misconfigured deployment
/// is loud instead of open.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class InternalOnlyAttribute : Attribute, IAsyncActionFilter
{
    public const string HeaderName = "X-Internal-Key";

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        IConfiguration configuration = context.HttpContext.RequestServices
            .GetRequiredService<IConfiguration>();

        string? expected = configuration["Internal:ApiKey"];

        if (string.IsNullOrWhiteSpace(expected))
        {
            context.Result = new ObjectResult(new
            {
                message = "Internal:ApiKey is not configured on this service, so internal "
                    + "endpoints are refused.",
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError,
            };

            return;
        }

        string? presented = context.HttpContext.Request.Headers[HeaderName];

        // Fixed-time comparison: these keys are long-lived, so a timing oracle
        // is worth denying even though the window is small.
        if (presented is null || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(presented),
                Encoding.UTF8.GetBytes(expected)))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        await next();
    }
}
