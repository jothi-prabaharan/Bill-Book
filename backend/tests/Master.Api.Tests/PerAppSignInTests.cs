using System.IdentityModel.Tokens.Jwt;
using Master.Api.Services;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Signing in is per app (H0.2, TK-43): the token names its app, carries only
/// that app's roles' permissions and that app's licence, and a refresh keeps it.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class PerAppSignInTests
{
    private readonly AdminFixture _admin;

    public PerAppSignInTests(AdminFixture admin) => _admin = admin;

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

    private static JwtSecurityToken Read(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    private static string Claim(string token, string type) => Read(token).Claims.Single(c => c.Type == type).Value;

    private static string[] Permissions(string token) =>
        Read(token).Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToArray();

    /// <summary>
    /// A customer with a lapsed RetailErp licence and a live Payroll one, and a
    /// user holding the RetailErp Accountant role and a Payroll role (which holds
    /// only settings.view) in one branch.
    /// </summary>
    private static async Task<(Guid UserId, Guid OrgId, Guid CustomerId)> SeedAsync(AdminDbContext db)
    {
        var customerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        db.Customers.Add(new Master.Entity.TableEntities.Customer
        {
            CustomerId = customerId,
            CustomerCode = Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString(),
            CountryPrefix = "IN",
            Name = "Two Apps Ltd",
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
            Name = "Chennai",
            BaseCurrency = "INR",
            FinancialYearStartMonth = 4,
            Status = TenantStatus.Active,
        });

        db.Licenses.Add(new License
        {
            CustomerId = customerId,
            App = App.RetailErp,
            LicenseType = LicenseType.Trial,
            StartDate = today.AddDays(-30),
            ExpiryDate = today.AddDays(-16),
            MaxUsers = 3,
            MaxOrganizations = 1,
            IsActive = true,
        });

        db.Licenses.Add(new License
        {
            CustomerId = customerId,
            App = App.Payroll,
            LicenseType = LicenseType.Standard,
            StartDate = today.AddDays(-1),
            ExpiryDate = today.AddYears(1),
            MaxUsers = 10,
            MaxOrganizations = 1,
            IsActive = true,
        });

        db.Users.Add(new User
        {
            UserId = userId,
            Email = $"priya-{suffix}@example.com",
            DisplayName = "Priya",
            PasswordHash = "x",
            IsActive = true,
            EmailConfirmed = true,
        });

        var payrollRole = new Role
        {
            CustomerId = customerId,
            SystemName = "Payroll Admin",
            DisplayName = "Payroll Admin",
            App = App.Payroll,
            IsActive = true,
        };
        db.Roles.Add(payrollRole);
        await db.SaveChangesAsync();

        int settingsView = await db.Permissions.Where(p => p.Code == "settings.view").Select(p => p.PermissionId).SingleAsync();
        db.RolePermissions.Add(new RolePermission { RoleId = payrollRole.RoleId, PermissionId = settingsView });

        Role accountant = await db.Roles.FirstAsync(r => r.IsSystemRole && r.SystemName == "Accountant" && r.App == App.RetailErp);
        db.UserOrganizationRoles.Add(new UserOrganizationRole { UserId = userId, OrgId = orgId, RoleId = accountant.RoleId, IsActive = true });
        db.UserOrganizationRoles.Add(new UserOrganizationRole { UserId = userId, OrgId = orgId, RoleId = payrollRole.RoleId, IsActive = true });

        await db.SaveChangesAsync();
        return (userId, orgId, customerId);
    }

    [SkippableFact]
    public async Task A_payroll_token_carries_its_app_its_roles_permissions_only_and_its_own_licence()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid orgId, _) = await SeedAsync(db);

        TokenResponse payroll = await Auth(db).SelectOrganizationAsync(userId, orgId, null, null, default, App.Payroll);

        Assert.Equal("Payroll", Claim(payroll.AccessToken, "app"));
        Assert.Equal(["settings.view"], Permissions(payroll.AccessToken));
        Assert.Equal("Active", payroll.LicenseStatus);
    }

    /// <summary>The card's Done-when line: an expired RetailErp licence leaves Payroll working.</summary>
    [SkippableFact]
    public async Task An_expired_retail_licence_leaves_payroll_working()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid orgId, Guid customerId) = await SeedAsync(db);
        AuthService auth = Auth(db);

        TokenResponse retail = await auth.SelectOrganizationAsync(userId, orgId, null, null, default, App.RetailErp);
        TokenResponse payroll = await auth.SelectOrganizationAsync(userId, orgId, null, null, default, App.Payroll);

        Assert.Equal("Expired", retail.LicenseStatus);
        Assert.Contains("accounting.view", Permissions(retail.AccessToken));
        Assert.Equal("Active", payroll.LicenseStatus);

        // Payroll is still paid for, so the account itself is not stamped expired.
        Assert.NotEqual(TenantStatus.Expired, (await db.Customers.AsNoTracking().SingleAsync(c => c.CustomerId == customerId)).Status);
    }

    [SkippableFact]
    public async Task Branches_are_offered_only_in_apps_where_the_user_holds_a_role()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid orgId, _) = await SeedAsync(db);
        AuthService auth = Auth(db);

        Assert.Equal([orgId], (await auth.AccessibleOrgsAsync(userId, default, App.Payroll)).Select(o => o.OrgId));
        Assert.Empty(await auth.AccessibleOrgsAsync(userId, default, App.Hrms));
        await Assert.ThrowsAsync<NoOrganizationAccessException>(
            () => auth.SelectOrganizationAsync(userId, orgId, null, null, default, App.Hrms));
    }

    [SkippableFact]
    public async Task A_refresh_mints_a_token_for_the_same_app()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid orgId, _) = await SeedAsync(db);
        AuthService auth = Auth(db);

        TokenResponse first = await auth.SelectOrganizationAsync(userId, orgId, null, null, default, App.Payroll);
        TokenResponse refreshed = await auth.RefreshAsync(first.RefreshToken, null, null, default);

        Assert.Equal("Payroll", Claim(refreshed.AccessToken, "app"));
        Assert.Equal(["settings.view"], Permissions(refreshed.AccessToken));
    }

    [SkippableFact]
    public async Task A_customer_with_no_licence_for_an_app_resolves_as_not_licensed()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (_, Guid orgId, _) = await SeedAsync(db);

        OrgContextResponse? hrms = await new OrgContextService(db, TimeProvider.System).ResolveAsync(orgId, default, App.Hrms);

        Assert.NotNull(hrms);
        Assert.Equal("NotLicensed", hrms.LicenseStatus);
        Assert.Null(hrms.LicenseExpiry);
    }
}
