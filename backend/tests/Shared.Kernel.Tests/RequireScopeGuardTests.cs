using System.Reflection;
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
/// Verifies branch business vertical and add-on scope guards (D-36, TK-128).
/// Enforces that tokens for one vertical or add-on cannot access unauthorized endpoints,
/// while supporting controller-level OR and action-level AND restrictions.
/// </summary>
public sealed class RequireScopeGuardTests
{
    private static ActionExecutingContext CreateContext(
        object controller,
        string actionName,
        BusinessType? businessType,
        AddOn addOns,
        bool authenticated = true,
        params object[] actionAttributes)
    {
        List<Claim> claims = [];
        if (businessType.HasValue)
        {
            claims.Add(new Claim(ScopeClaims.BusinessTypeClaim, businessType.Value.ToString()));
        }

        claims.Add(new Claim(ScopeClaims.AddOnsClaim, ((int)addOns).ToString()));

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "Bearer" : null)),
        };

        var descriptor = new ActionDescriptor
        {
            EndpointMetadata = [.. actionAttributes],
        };

        return new ActionExecutingContext(
            new ActionContext(http, new RouteData(), descriptor),
            [],
            new Dictionary<string, object?>(),
            controller: controller);
    }

    [Fact]
    public void RequireBusinessType_allows_matching_vertical()
    {
        var attr = new RequireBusinessTypeAttribute(BusinessType.Retail, BusinessType.Pharma);
        Assert.True(attr.Allows(BusinessType.Retail));
        Assert.True(attr.Allows(BusinessType.Pharma));
        Assert.False(attr.Allows(BusinessType.School));
        Assert.False(attr.Allows(null));
    }

    [Fact]
    public void RequireBusinessType_parameterless_requires_any_business_type()
    {
        var attr = new RequireBusinessTypeAttribute();
        Assert.True(attr.Allows(BusinessType.Retail));
        Assert.True(attr.Allows(BusinessType.School));
        Assert.False(attr.Allows(null));
    }

    [Fact]
    public void RequireBusinessType_with_allow_standalone_allows_null_business_type()
    {
        var attr = new RequireBusinessTypeAttribute { AllowStandalone = true };
        Assert.True(attr.Allows(BusinessType.Retail));
        Assert.True(attr.Allows(null));
    }

    [Fact]
    public void RequireAddOn_allows_when_token_has_flag()
    {
        var attr = new RequireAddOnAttribute(AddOn.Pos);
        Assert.True(attr.Allows(AddOn.Pos));
        Assert.True(attr.Allows(AddOn.Pos | AddOn.Budget));
        Assert.False(attr.Allows(AddOn.None));
        Assert.False(attr.Allows(AddOn.Budget));
    }

    [Fact]
    public void RequireAddOn_multi_flag_allows_any_matching_flag()
    {
        var attr = new RequireAddOnAttribute(AddOn.Hrms | AddOn.Payroll);
        Assert.True(attr.Allows(AddOn.Hrms));
        Assert.True(attr.Allows(AddOn.Payroll));
        Assert.True(attr.Allows(AddOn.Hrms | AddOn.Payroll));
        Assert.False(attr.Allows(AddOn.Pos));
        Assert.False(attr.Allows(AddOn.None));
    }

    [Fact]
    public void Unauthenticated_request_is_not_blocked_by_scope_guard()
    {
        var controller = new SampleRetailController();
        var guard = new RequireBusinessTypeAttribute(BusinessType.Retail);
        ActionExecutingContext context = CreateContext(controller, "Index", businessType: null, addOns: AddOn.None, authenticated: false);

        guard.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void InternalOnly_controller_bypasses_scope_guard()
    {
        var controller = new SampleInternalController();
        var guard = new RequireBusinessTypeAttribute(BusinessType.Retail);
        ActionExecutingContext context = CreateContext(
            controller, "Index", businessType: BusinessType.School, addOns: AddOn.None, authenticated: true,
            new InternalOnlyAttribute());

        guard.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void Controller_level_OR_semantics_allows_matching_vertical_or_addon()
    {
        var controller = new SampleDualScopeController();
        var btGuard = new RequireBusinessTypeAttribute(BusinessType.School);
        var aoGuard = new RequireAddOnAttribute(AddOn.Hrms | AddOn.Payroll);

        // Case 1: School branch without HRMS/Payroll -> Allowed (matches BusinessType)
        ActionExecutingContext ctx1 = CreateContext(controller, "Index", BusinessType.School, AddOn.None);
        btGuard.OnActionExecuting(ctx1);
        Assert.Null(ctx1.Result);

        // Case 2: Standalone HRMS branch without BusinessType -> Allowed (matches AddOn)
        ActionExecutingContext ctx2 = CreateContext(controller, "Index", null, AddOn.Hrms);
        btGuard.OnActionExecuting(ctx2);
        Assert.Null(ctx2.Result);

        // Case 3: General retail branch without HRMS/Payroll -> Forbidden
        ActionExecutingContext ctx3 = CreateContext(controller, "Index", BusinessType.Retail, AddOn.Pos);
        btGuard.OnActionExecuting(ctx3);
        Assert.IsType<ForbidResult>(ctx3.Result);
    }

    [Fact]
    public void Action_level_AND_semantics_restricts_even_when_controller_vertical_matches()
    {
        var controller = new SampleRetailController();
        var btGuard = new RequireBusinessTypeAttribute(BusinessType.Retail);
        var aoActionGuard = new RequireAddOnAttribute(AddOn.EInvoicing);

        // Retail branch without EInvoicing add-on -> Action guard refuses
        ActionExecutingContext ctx1 = CreateContext(controller, "EwayBill", BusinessType.Retail, AddOn.None, true, aoActionGuard);
        aoActionGuard.OnActionExecuting(ctx1);
        Assert.IsType<ForbidResult>(ctx1.Result);

        // Retail branch WITH EInvoicing add-on -> Allowed
        ActionExecutingContext ctx2 = CreateContext(controller, "EwayBill", BusinessType.Retail, AddOn.EInvoicing, true, aoActionGuard);
        btGuard.OnActionExecuting(ctx2);
        Assert.Null(ctx2.Result);
        aoActionGuard.OnActionExecuting(ctx2);
        Assert.Null(ctx2.Result);

        // Non-retail branch WITH EInvoicing add-on -> Forbidden (controller-level vertical check fails)
        ActionExecutingContext ctx3 = CreateContext(controller, "EwayBill", BusinessType.School, AddOn.EInvoicing, true, aoActionGuard);
        btGuard.OnActionExecuting(ctx3);
        Assert.IsType<ForbidResult>(ctx3.Result);
    }

    [Fact]
    public void ScopeClaims_extracts_vertical_and_addons_or_fallbacks()
    {
        // 1. Explicit claims
        var user1 = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ScopeClaims.BusinessTypeClaim, "Pharma"),
            new Claim(ScopeClaims.AddOnsClaim, ((int)(AddOn.Pos | AddOn.Budget)).ToString()),
        ]));
        Assert.Equal(BusinessType.Pharma, ScopeClaims.BusinessTypeOf(user1));
        Assert.Equal(AddOn.Pos | AddOn.Budget, ScopeClaims.AddOnsOf(user1));

        // 2. Standalone HRMS token (no business type claim, has add_ons claim)
        var user2 = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ScopeClaims.AddOnsClaim, ((int)AddOn.Hrms).ToString()),
        ]));
        Assert.Null(ScopeClaims.BusinessTypeOf(user2));
        Assert.Equal(AddOn.Hrms, ScopeClaims.AddOnsOf(user2));

        // 3. A token with no scope claims at all reads as a Retail branch with
        //    no add-ons. The app claim it might once have carried is gone (TK-136).
        var user3 = new ClaimsPrincipal(new ClaimsIdentity([new Claim("app", "School")]));
        Assert.Equal(BusinessType.Retail, ScopeClaims.BusinessTypeOf(user3));
        Assert.Equal(AddOn.None, ScopeClaims.AddOnsOf(user3));
    }

    [Fact]
    public void EndpointGuardAudit_WithoutBusinessTypeOrAddOn_flags_unguarded_controllers()
    {
        var open = EndpointGuardAudit.WithoutBusinessTypeOrAddOn(typeof(RequireScopeGuardTests).Assembly);
        Assert.Contains($"{nameof(SampleUnguardedController)}.{nameof(SampleUnguardedController.Foo)}", open);

        var exempt = EndpointGuardAudit.WithoutBusinessTypeOrAddOn(
            typeof(RequireScopeGuardTests).Assembly,
            nameof(SampleUnguardedController));
        Assert.DoesNotContain($"{nameof(SampleUnguardedController)}.{nameof(SampleUnguardedController.Foo)}", exempt);
    }

    // Dummy controllers for testing reflection-based attributes
    [RequireBusinessType(BusinessType.Retail)]
    private sealed class SampleRetailController : ControllerBase { }

    [RequireBusinessType(BusinessType.School)]
    [RequireAddOn(AddOn.Hrms | AddOn.Payroll)]
    private sealed class SampleDualScopeController : ControllerBase { }

    [InternalOnly]
    private sealed class SampleInternalController : ControllerBase { }

    private sealed class SampleUnguardedController : ControllerBase
    {
        [HttpGet("foo")]
        public IActionResult Foo() => Ok();
    }
}
