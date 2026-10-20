using Master.Api.Services;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Master.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// The branch list behind login and the switcher carries what the switcher
/// shows beside each branch: its organisation, trade, currency, GSTIN and city
/// (TK-189), read in one query rather than one resolve per branch.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class AccessibleOrgsTests
{
    private readonly AdminFixture _admin;

    public AccessibleOrgsTests(AdminFixture admin) => _admin = admin;

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
            new OtpService(), new OrgContextRepository(db, TimeProvider.System), new NoEmail(), TimeProvider.System,
            new RecordingMobileMessages());
    }

    /// <summary>
    /// One customer with a registered Pharma branch and a branch whose
    /// business type is unset; the user is Accountant in the first and Viewer
    /// in the second.
    /// </summary>
    private static async Task<(Guid UserId, Guid PharmaId, Guid UntypedId)> SeedAsync(AdminDbContext db)
    {
        var customerId = Guid.NewGuid();
        var pharmaId = Guid.NewGuid();
        var untypedId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        string suffix = Guid.NewGuid().ToString("N")[..8];

        db.Customers.Add(new Master.Entity.TableEntities.Customer
        {
            CustomerId = customerId,
            CustomerCode = Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString(),
            CountryPrefix = "IN",
            Name = "Kumar Pharma Ltd",
            BillingEmail = $"billing-{suffix}@example.com",
            Status = TenantStatus.Active,
            PlanTier = PlanTier.Standard,
            DatabaseName = "IN000001",
        });

        db.Organizations.Add(new Organization
        {
            OrgId = pharmaId,
            CustomerId = customerId,
            OrgCode = $"P{suffix}",
            Name = "Anna Nagar",
            BaseCurrency = "INR",
            FinancialYearStartMonth = 4,
            Status = TenantStatus.Active,
            BusinessType = BusinessType.Pharma,
            Vertical = Vertical.Pharma,
            Gstin = "33AAAAA0000A1Z5",
            City = "Chennai",
        });

        db.Organizations.Add(new Organization
        {
            OrgId = untypedId,
            CustomerId = customerId,
            OrgCode = $"U{suffix}",
            Name = "Dubai",
            BaseCurrency = "AED",
            FinancialYearStartMonth = 1,
            Status = TenantStatus.Active,
            BusinessType = null,
        });

        db.Users.Add(new User
        {
            UserId = userId,
            Email = $"kumar-{suffix}@example.com",
            DisplayName = "Kumar",
            PasswordHash = "x",
            IsActive = true,
            EmailConfirmed = true,
        });
        await db.SaveChangesAsync();

        Role accountant = db.Roles.First(r => r.IsSystemRole && r.SystemName == "Accountant");
        Role viewer = db.Roles.First(r => r.IsSystemRole && r.SystemName == "Viewer");
        db.UserOrganizationRoles.Add(new UserOrganizationRole { UserId = userId, OrgId = pharmaId, RoleId = accountant.RoleId, IsActive = true });
        db.UserOrganizationRoles.Add(new UserOrganizationRole { UserId = userId, OrgId = untypedId, RoleId = viewer.RoleId, IsActive = true });
        await db.SaveChangesAsync();

        return (userId, pharmaId, untypedId);
    }

    [SkippableFact]
    public async Task Each_branch_carries_its_organisation_trade_currency_gstin_and_city()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid pharmaId, _) = await SeedAsync(db);

        IReadOnlyList<AccessibleOrgDto> orgs = await Auth(db).AccessibleOrgsAsync(userId, CancellationToken.None);

        AccessibleOrgDto pharma = Assert.Single(orgs, o => o.OrgId == pharmaId);
        Assert.Equal("Anna Nagar", pharma.OrgName);
        Assert.Equal("Accountant", pharma.RoleName);
        Assert.Equal("Kumar Pharma Ltd", pharma.CustomerName);
        Assert.Equal("Pharma", pharma.BusinessType);
        Assert.Equal("INR", pharma.BaseCurrency);
        Assert.Equal("33AAAAA0000A1Z5", pharma.Gstin);
        Assert.Equal("Chennai", pharma.City);
    }

    [SkippableFact]
    public async Task A_branch_with_no_business_type_reads_as_Retail_and_an_unregistered_one_has_no_gstin()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, _, Guid untypedId) = await SeedAsync(db);

        IReadOnlyList<AccessibleOrgDto> orgs = await Auth(db).AccessibleOrgsAsync(userId, CancellationToken.None);

        AccessibleOrgDto untyped = Assert.Single(orgs, o => o.OrgId == untypedId);
        Assert.Equal("Dubai", untyped.OrgName);
        Assert.Equal("Viewer", untyped.RoleName);
        Assert.Equal("Retail", untyped.BusinessType);
        Assert.Equal("AED", untyped.BaseCurrency);
        Assert.Null(untyped.Gstin);
        Assert.Null(untyped.City);
    }

    [SkippableFact]
    public async Task Every_branch_the_user_holds_a_role_in_is_listed_once()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid pharmaId, Guid untypedId) = await SeedAsync(db);

        IReadOnlyList<AccessibleOrgDto> orgs = await Auth(db).AccessibleOrgsAsync(userId, CancellationToken.None);

        Assert.Equal(new[] { pharmaId, untypedId }.OrderBy(id => id), orgs.Select(o => o.OrgId).OrderBy(id => id));
        Assert.All(orgs, o => Assert.Equal("Kumar Pharma Ltd", o.CustomerName));
    }
}
