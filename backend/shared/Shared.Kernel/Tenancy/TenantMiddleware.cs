using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Shared.Kernel.Tenancy;

/// <summary>
/// Populates the request's tenant context from the JWT. Runs after
/// authentication and before anything touches a per-customer DbContext.
/// </summary>
public sealed class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantContext tenant)
    {
        if (tenant is TenantContext writable && context.User.Identity?.IsAuthenticated == true)
        {
            writable.CustomerId = Claim(context.User, "customer_id");
            writable.OrgId = Claim(context.User, "org_id");
            writable.Permissions = new HashSet<string>(
                context.User.FindAll("permission").Select(c => c.Value),
                StringComparer.OrdinalIgnoreCase);
        }

        await _next(context);
    }

    private static Guid? Claim(ClaimsPrincipal user, string type)
    {
        string? value = user.FindFirst(type)?.Value;
        return Guid.TryParse(value, out Guid id) ? id : null;
    }
}
