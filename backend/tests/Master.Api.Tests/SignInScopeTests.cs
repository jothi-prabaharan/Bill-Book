using System.IdentityModel.Tokens.Jwt;
using Master.Api.Services;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Master.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Signing in is per branch, not per app (TK-136, D-50): the token carries the
/// branch's business type and add-ons, every role the user holds there, and the
/// customer's one licence — and a refresh keeps all of it.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class SignInScopeTests
{
    private readonly AdminFixture _admin;

    public SignInScopeTests(AdminFixture admin) => _admin = admin;

    private sealed class NoEmail : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static AuthRepository Auth(AdminDbContext db)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new JwtOptionsRepository
        {
            Issuer = "bill-book",
            Audience = "bill-book",
            SigningKey = "a-test-signing-key-that-is-long-enough-for-hmac-sha256",
            RefreshTokenDays = 7,
        });

        return new AuthRepository(
            db, new BcryptPasswordHasher(), new JwtTokenRepository(options, TimeProvider.System),
            new OtpService(), new OrgContextRepository(db, TimeProvider.System), new NoEmail(), TimeProvider.System);
    }

    private static JwtSecurityToken Read(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    private static string? Claim(string token, string type) => Read(token).Claims.SingleOrDefault(c => c.Type == type)?.Value;

    private static string[] Permissions(string token) =>
        Read(token).Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToArray();

    /// <summary>
    /// A Pharma branch with the Payroll add-on, and a user holding the system
    /// Accountant role and a customer role with only settings.view in it.
    /// </summary>
    private static async Task<(Guid UserId, Guid OrgId)> SeedAsync(AdminDbContext db)
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
            Name = "Scope Pharma Ltd",
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
            BusinessType = BusinessType.Pharma,
            Vertical = Vertical.Pharma,
            AddOns = AddOn.Payroll,
        });

        db.Licenses.Add(new License
        {
            CustomerId = customerId,
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

        var settingsRole = new Role
        {
            CustomerId = customerId,
            SystemName = "Settings Viewer",
            DisplayName = "Settings Viewer",
            IsActive = true,
        };
        db.Roles.Add(settingsRole);
        await db.SaveChangesAsync();

        int settingsView = await db.Permissions.Where(p => p.Code == "settings.view").Select(p => p.PermissionId).SingleAsync();
        db.RolePermissions.Add(new RolePermission { RoleId = settingsRole.RoleId, PermissionId = settingsView });

        Role accountant = await db.Roles.FirstAsync(r => r.IsSystemRole && r.SystemName == "Accountant");
        db.UserOrganizationRoles.Add(new UserOrganizationRole { UserId = userId, OrgId = orgId, RoleId = accountant.RoleId, IsActive = true });
        db.UserOrganizationRoles.Add(new UserOrganizationRole { UserId = userId, OrgId = orgId, RoleId = settingsRole.RoleId, IsActive = true });

        await db.SaveChangesAsync();
        return (userId, orgId);
    }

    [SkippableFact]
    public async Task The_token_carries_the_branch_scope_every_roles_permissions_and_no_app()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid orgId) = await SeedAsync(db);

        TokenResponse token = await Auth(db).SelectOrganizationAsync(userId, orgId, null, null, default);

        Assert.Equal("Pharma", Claim(token.AccessToken, "business_type"));
        Assert.Equal(((int)AddOn.Payroll).ToString(), Claim(token.AccessToken, "add_ons"));
        Assert.Null(Claim(token.AccessToken, "app"));
        Assert.Contains("accounting.view", Permissions(token.AccessToken));
        Assert.Contains("settings.view", Permissions(token.AccessToken));
        Assert.Equal("Active", token.LicenseStatus);
    }

    [SkippableFact]
    public async Task A_branch_is_offered_once_whatever_roles_are_held_in_it()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid orgId) = await SeedAsync(db);

        Assert.Equal([orgId], (await Auth(db).AccessibleOrgsAsync(userId, default)).Select(o => o.OrgId));
    }

    [SkippableFact]
    public async Task A_refresh_keeps_the_branch_scope()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid orgId) = await SeedAsync(db);
        AuthRepository auth = Auth(db);

        TokenResponse first = await auth.SelectOrganizationAsync(userId, orgId, null, null, default);
        TokenResponse refreshed = await auth.RefreshAsync(first.RefreshToken, null, null, default);

        Assert.Equal("Pharma", Claim(refreshed.AccessToken, "business_type"));
        Assert.Equal(Permissions(first.AccessToken).Order(), Permissions(refreshed.AccessToken).Order());
    }
}
