using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// The service-side app check (H0.2, TK-43): a token for one app cannot call
/// another app's routes, whatever permissions it holds.
/// </summary>
public sealed class RequireAppTests
{
    private static ActionExecutingContext Executing(string? app, bool authenticated = true, params RequireAppAttribute[] declared)
    {
        List<Claim> claims = [new("permission", "settings.view")];
        if (app is not null)
        {
            claims.Add(new Claim(RequireAppAttribute.ClaimType, app));
        }

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "test" : null)),
        };

        var descriptor = new ActionDescriptor { EndpointMetadata = [.. declared] };

        return new ActionExecutingContext(
            new ActionContext(http, new RouteData(), descriptor),
            [],
            new Dictionary<string, object?>(),
            controller: new object());
    }

    private static IActionResult? Run(RequireAppAttribute guard, string? app, bool authenticated = true)
    {
        ActionExecutingContext context = Executing(app, authenticated, guard);
        guard.OnActionExecuting(context);
        return context.Result;
    }

    [Fact]
    public void An_hrms_token_calling_a_retail_route_is_refused() =>
        Assert.IsType<ForbidResult>(Run(new RequireAppAttribute(App.RetailErp), "Hrms"));

    [Fact]
    public void A_payroll_token_reads_employees_but_not_recruitment()
    {
        // The employee master serves HRMS and Payroll; recruitment is HRMS's alone.
        Assert.Null(Run(new RequireAppAttribute(App.Hrms | App.Payroll), "Payroll"));
        Assert.IsType<ForbidResult>(Run(new RequireAppAttribute(App.Hrms), "Payroll"));
    }

    [Fact]
    public void A_shared_route_takes_every_apps_token()
    {
        foreach (string app in new[] { "RetailErp", "School", "Hrms", "Payroll" })
        {
            Assert.Null(Run(new RequireAppAttribute(App.All), app));
        }
    }

    [Fact]
    public void A_token_with_no_app_claim_is_a_retail_token()
    {
        Assert.Null(Run(new RequireAppAttribute(App.RetailErp), app: null));
        Assert.IsType<ForbidResult>(Run(new RequireAppAttribute(App.Payroll), app: null));
    }

    [Theory]
    [InlineData("All")]
    [InlineData("15")]
    [InlineData("Accounting")]
    public void A_claim_that_names_no_single_app_is_refused(string claim) =>
        Assert.IsType<ForbidResult>(Run(new RequireAppAttribute(App.All), claim));

    [Fact]
    public void An_unauthenticated_request_is_left_to_the_other_guards() =>
        Assert.Null(Run(new RequireAppAttribute(App.Payroll), "RetailErp", authenticated: false));

    [Fact]
    public void An_action_declaration_replaces_the_controllers()
    {
        var controllerLevel = new RequireAppAttribute(App.RetailErp);
        var actionLevel = new RequireAppAttribute(App.All);
        ActionExecutingContext context = Executing("Payroll", true, controllerLevel, actionLevel);

        controllerLevel.OnActionExecuting(context);
        actionLevel.OnActionExecuting(context);

        Assert.Null(context.Result);
    }
}
