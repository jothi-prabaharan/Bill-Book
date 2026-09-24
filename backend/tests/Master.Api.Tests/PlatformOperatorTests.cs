using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using Master.Api.Controllers;
using Master.Api.Services;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Platform operators (D-01, TK-13): <c>platform.*</c> comes from
/// <c>mst.Users.IsPlatformOperator</c> and from nothing else — never a role, not
/// even the Owner's, because <c>Role</c> rows are shared across customers.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class PlatformOperatorTests
{
    private readonly AdminFixture _admin;

    public PlatformOperatorTests(AdminFixture admin) => _admin = admin;

    private sealed class NoEmail : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static AuthService Auth(AdminDbContext db)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Issuer = "bill-book",
            Audience = "bill-book",
            SigningKey = "a-test-signing-key-that-is-long-enough-for-hmac-sha256",
            RefreshTokenDays = 7,
        });

        return new AuthService(
            db, new BcryptPasswordHasher(), new JwtTokenService(options, TimeProvider.System),
            new OtpService(), new OrgContextService(db, TimeProvider.System), new NoEmail(), TimeProvider.System);
    }

    private static string[] Permissions(string accessToken) =>
        new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToArray();

    /// <summary>A customer, a branch and a user holding <paramref name="roleSystemName"/> in it.</summary>
    private static async Task<(Guid UserId, Guid OrgId, string Email)> SeedUserAsync(
        AdminDbContext db, bool isOperator, string roleSystemName = "Owner")
    {
        var customerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string email = $"user-{suffix}@example.com";

        db.Customers.Add(new Master.Entity.TableEntities.Customer
        {
            CustomerId = customerId,
            CustomerCode = Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString(),
            CountryPrefix = "IN",
            Name = "Test Customer",
            BillingEmail = $"billing-{suffix}@example.com",
            Status = TenantStatus.Active,
            PlanTier = PlanTier.Standard,
            DatabaseName = "IN000001",
        });

        db.Organizations.Add(new Organization
        {
            OrgId = orgId,
            CustomerId = customerId,
            OrgCode = $"B{suffix}",
            Name = "Head Office",
            BaseCurrency = "INR",
            FinancialYearStartMonth = 4,
            Status = TenantStatus.Active,
        });

        db.Licenses.Add(new License
        {
            CustomerId = customerId,
            LicenseType = LicenseType.Standard,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1),
            MaxUsers = 10,
            MaxOrganizations = 5,
            IsActive = true,
        });

        db.Users.Add(new User
        {
            UserId = userId,
            Email = email,
            DisplayName = "Test User",
            PasswordHash = "x",
            IsActive = true,
            EmailConfirmed = true,
            IsPlatformOperator = isOperator,
        });

        await db.SaveChangesAsync();

        Role role = await db.Roles.FirstAsync(r => r.IsSystemRole && r.SystemName == roleSystemName);

        db.UserOrganizationRoles.Add(new UserOrganizationRole
        {
            UserId = userId,
            OrgId = orgId,
            RoleId = role.RoleId,
            IsActive = true,
        });

        await db.SaveChangesAsync();
        return (userId, orgId, email);
    }

    [SkippableFact]
    public async Task A_customers_owner_never_gets_platform_permissions()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid orgId, _) = await SeedUserAsync(db, isOperator: false);

        TokenResponse token = await Auth(db).SelectOrganizationAsync(userId, orgId, null, null, default);
        string[] permissions = Permissions(token.AccessToken);

        Assert.NotEmpty(permissions);
        Assert.DoesNotContain(permissions, p => p.StartsWith("platform.", StringComparison.Ordinal));
    }

    [SkippableFact]
    public async Task An_operator_gets_every_platform_permission_beside_their_role()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid orgId, _) = await SeedUserAsync(db, isOperator: true, roleSystemName: "Viewer");

        TokenResponse token = await Auth(db).SelectOrganizationAsync(userId, orgId, null, null, default);
        string[] permissions = Permissions(token.AccessToken);

        List<string> catalogue = await db.Permissions
            .Where(p => p.Module == "platform")
            .Select(p => p.Code)
            .ToListAsync();

        Assert.NotEmpty(catalogue);
        Assert.All(catalogue, code => Assert.Contains(code, permissions));

        // The role's own permissions are still there.
        Assert.Contains(permissions, p => p.EndsWith(".view", StringComparison.Ordinal) && !p.StartsWith("platform.", StringComparison.Ordinal));
    }

    [SkippableFact]
    public async Task A_platform_permission_on_a_role_is_ignored_for_a_non_operator()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid orgId, _) = await SeedUserAsync(db, isOperator: false);

        // However it got there — a bad seed, a hand edit — a role row granting
        // platform.view grants nothing.
        UserOrganizationRole assignment = await db.UserOrganizationRoles.FirstAsync(u => u.UserId == userId);
        Permission view = await db.Permissions.FirstAsync(p => p.Code == "platform.view");

        await using AdminDbContext scratch = _admin.CreateContext();
        await using var tx = await scratch.Database.BeginTransactionAsync();
        scratch.RolePermissions.Add(new RolePermission
        {
            RolePermissionId = (await scratch.RolePermissions.MaxAsync(r => r.RolePermissionId)) + 1,
            RoleId = assignment.RoleId,
            PermissionId = view.PermissionId,
        });
        await scratch.SaveChangesAsync();

        // Read inside the same transaction, so the shared Owner role is never
        // left holding it for any other test.
        TokenResponse token = await Auth(scratch).SelectOrganizationAsync(userId, orgId, null, null, default);
        await tx.RollbackAsync();

        Assert.DoesNotContain("platform.view", Permissions(token.AccessToken));
    }

    [SkippableFact]
    public async Task A_revoked_operator_loses_platform_access_at_the_next_refresh()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        (Guid userId, Guid orgId, _) = await SeedWith(isOperator: true);
        (Guid otherOperator, _, _) = await SeedWith(isOperator: true);

        TokenResponse first;
        await using (AdminDbContext db = _admin.CreateContext())
        {
            first = await Auth(db).SelectOrganizationAsync(userId, orgId, null, null, default);
        }

        Assert.Contains("platform.view", Permissions(first.AccessToken));

        await using (AdminDbContext db = _admin.CreateContext())
        {
            PlatformOperatorOutcome revoked = await new PlatformOperatorService(db)
                .SetAsync(userId, isOperator: false, callerId: otherOperator, default);

            Assert.Equal(PlatformOperatorOutcome.Ok, revoked);
        }

        await using (AdminDbContext db = _admin.CreateContext())
        {
            TokenResponse refreshed = await Auth(db).RefreshAsync(first.RefreshToken, null, null, default);
            Assert.DoesNotContain(Permissions(refreshed.AccessToken), p => p.StartsWith("platform.", StringComparison.Ordinal));
        }
    }

    [SkippableFact]
    public async Task An_operator_cannot_revoke_themselves_and_an_unknown_user_is_not_found()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        (Guid userId, _, _) = await SeedWith(isOperator: true);

        await using AdminDbContext db = _admin.CreateContext();
        var service = new PlatformOperatorService(db);

        Assert.Equal(PlatformOperatorOutcome.CannotRevokeSelf, await service.SetAsync(userId, false, userId, default));
        Assert.Equal(PlatformOperatorOutcome.NotFound, await service.SetAsync(Guid.NewGuid(), true, userId, default));
        Assert.True((await db.Users.AsNoTracking().SingleAsync(u => u.UserId == userId)).IsPlatformOperator);
    }

    [SkippableFact]
    public async Task Bootstrap_grants_the_named_addresses_whatever_their_case_and_nobody_else()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        (Guid named, _, string namedEmail) = await SeedWith(isOperator: false);
        (Guid other, _, _) = await SeedWith(isOperator: false);

        await using AdminDbContext db = _admin.CreateContext();

        int granted = await PlatformOperatorService.ApplyBootstrapAsync(
            db, PlatformOperatorService.BootstrapEmails(Config(("Bootstrap:OperatorEmails", $" {namedEmail.ToUpperInvariant()} ; nobody@example.com"))),
            default);

        Assert.Equal(1, granted);
        Assert.True((await db.Users.AsNoTracking().SingleAsync(u => u.UserId == named)).IsPlatformOperator);
        Assert.False((await db.Users.AsNoTracking().SingleAsync(u => u.UserId == other)).IsPlatformOperator);

        // Idempotent: a second start grants nobody new.
        Assert.Equal(0, await PlatformOperatorService.ApplyBootstrapAsync(db, [namedEmail], default));
    }

    [Fact]
    public void Bootstrap_emails_read_from_a_list_or_an_array_trimmed_and_lower_cased()
    {
        Assert.Equal(
            ["a@x.in", "b@x.in"],
            PlatformOperatorService.BootstrapEmails(Config(("Bootstrap:OperatorEmails", " A@x.in,b@X.in ;; a@x.in"))));

        Assert.Equal(
            ["a@x.in", "b@x.in"],
            PlatformOperatorService.BootstrapEmails(Config(
                ("Bootstrap:OperatorEmails:0", "A@x.in"),
                ("Bootstrap:OperatorEmails:1", "b@x.in"))));

        Assert.Empty(PlatformOperatorService.BootstrapEmails(Config()));
    }

    [Fact]
    public void The_grant_endpoint_needs_platform_edit_and_refuses_a_tenant_token()
    {
        MethodInfo set = typeof(PlatformOperatorsController).GetMethod(nameof(PlatformOperatorsController.Set))!;
        RequirePermissionAttribute guard = set.GetCustomAttribute<RequirePermissionAttribute>()!;

        Assert.Equal("platform.edit", guard.Permission);

        // An Owner's token: every tenant permission, no platform one.
        ActionExecutingContext owner = Executing("sales.edit", "settings.edit", "users.edit");
        guard.OnActionExecuting(owner);
        Assert.IsType<ForbidResult>(owner.Result);

        ActionExecutingContext operatorToken = Executing("platform.view", "platform.edit");
        guard.OnActionExecuting(operatorToken);
        Assert.Null(operatorToken.Result);
    }

    private async Task<(Guid UserId, Guid OrgId, string Email)> SeedWith(bool isOperator)
    {
        await using AdminDbContext db = _admin.CreateContext();
        return await SeedUserAsync(db, isOperator);
    }

    private static IConfiguration Config(params (string Key, string Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

    private static ActionExecutingContext Executing(params string[] permissions)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                permissions.Select(p => new Claim("permission", p)), "test")),
        };

        return new ActionExecutingContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()),
            [],
            new Dictionary<string, object?>(),
            controller: new object());
    }
}
