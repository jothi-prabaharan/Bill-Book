using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Kernel.Internal;
using Shared.Kernel.Security;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// An API key acts with its role's <c>{module}.{action}</c> permissions and
/// nothing more (D-07, TK-29): a key whose role holds <c>sales.view</c> can
/// list invoices and is refused posting one, and a <c>platform.*</c> code never
/// reaches the principal whatever Master sends.
/// </summary>
public sealed class ApiKeyPermissionTests
{
    private sealed class StubValidator(params string[] permissions) : IApiKeyValidator
    {
        public Task<ApiKeyValidationResult> ValidateAsync(string apiKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ApiKeyValidationResult
            {
                IsValid = true,
                CustomerId = Guid.NewGuid(),
                OrgId = Guid.NewGuid(),
                ApiClientId = Guid.NewGuid(),
                ClientName = "Shop sync",
                Permissions = [.. permissions],
            });
    }

    private sealed class Monitor : IOptionsMonitor<ApiKeyAuthenticationOptions>
    {
        public ApiKeyAuthenticationOptions CurrentValue { get; } = new();
        public ApiKeyAuthenticationOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<ApiKeyAuthenticationOptions, string?> listener) => null;
    }

    private static async Task<ClaimsPrincipal> SignInAsync(params string[] permissions)
    {
        var handler = new ApiKeyAuthenticationHandler(
            new Monitor(), NullLoggerFactory.Instance, UrlEncoder.Default, new StubValidator(permissions));

        var http = new DefaultHttpContext();
        http.Request.Headers["X-Api-Key"] = "bb_key";

        await handler.InitializeAsync(
            new AuthenticationScheme(ApiKeyAuthenticationOptions.DefaultScheme, null, typeof(ApiKeyAuthenticationHandler)),
            http);

        AuthenticateResult result = await handler.AuthenticateAsync();
        Assert.True(result.Succeeded);
        return result.Principal!;
    }

    /// <summary>What <c>[RequireModulePermission("sales")]</c> decides for this principal on one route.</summary>
    private static IActionResult? Decide(ClaimsPrincipal user, string method, string? declaredAction)
    {
        var http = new DefaultHttpContext { User = user };
        http.Request.Method = method;

        var descriptor = new ActionDescriptor { EndpointMetadata = [] };
        if (declaredAction is not null)
        {
            descriptor.EndpointMetadata.Add(new PermissionActionAttribute(declaredAction));
        }

        var context = new ActionExecutingContext(
            new ActionContext(http, new RouteData(), descriptor),
            [],
            new Dictionary<string, object?>(),
            controller: new object());

        new RequireModulePermissionAttribute("sales").OnActionExecuting(context);
        return context.Result;
    }

    [Fact]
    public async Task A_key_whose_role_has_sales_view_lists_invoices_and_cannot_post_one()
    {
        ClaimsPrincipal key = await SignInAsync("sales.view");

        // GET api/sales/invoices — sales.view.
        Assert.Null(Decide(key, "GET", declaredAction: null));

        // POST api/sales/invoices/{id}/post — sales.approve.
        Assert.IsType<ForbidResult>(Decide(key, "POST", declaredAction: "approve"));

        // POST api/sales/invoices — sales.edit.
        Assert.IsType<ForbidResult>(Decide(key, "POST", declaredAction: null));
    }

    [Fact]
    public async Task Every_role_permission_becomes_a_claim_and_platform_codes_never_do()
    {
        ClaimsPrincipal key = await SignInAsync("sales.view", "sales.approve", "platform.edit", "PLATFORM.view", "sales.view");

        string[] claims = key.FindAll("permission").Select(c => c.Value).ToArray();

        Assert.Equal(["sales.view", "sales.approve"], claims);
        Assert.Equal("ApiClient", key.FindFirst("role")!.Value);
    }

    [Fact]
    public async Task A_key_with_no_role_permissions_is_refused_everywhere()
    {
        // Every key minted before TK-29 holds role 0, which grants nothing.
        ClaimsPrincipal key = await SignInAsync();

        Assert.Empty(key.FindAll("permission"));
        Assert.IsType<ForbidResult>(Decide(key, "GET", declaredAction: null));
    }
}
