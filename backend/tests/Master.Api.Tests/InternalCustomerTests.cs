using Master.Entity.Enums;
using Master.Entity.TableEntities;
using Master.Repository;
using Master.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// The Internal customer (D-44, TK-153): the bootstrap customer is our own
/// organisation with a Service head office, its add-ons come from
/// configuration within what Service allows, and an older install's
/// bootstrap customer is converted once and never again.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class InternalCustomerTests
{
    private readonly AdminFixture _admin;

    public InternalCustomerTests(AdminFixture admin) => _admin = admin;

    [Theory]
    [InlineData(null, AddOn.None, false)]
    [InlineData("", AddOn.None, false)]
    [InlineData("Hrms, Payroll", AddOn.Hrms | AddOn.Payroll, false)]
    [InlineData("Crm", AddOn.Crm, false)]
    // A Service branch has no till and no e-invoicing (AddOnRules.Allowed).
    [InlineData("Pos", AddOn.None, true)]
    [InlineData("Hrms, Pos", AddOn.Hrms, true)]
    [InlineData("not-an-add-on", AddOn.None, true)]
    public void Add_ons_come_from_configuration_within_what_service_allows(string? setting, AddOn expected, bool reported)
    {
        AddOn addOns = InternalCustomerRepository.AddOnsFrom(setting, out string? problem);

        Assert.Equal(expected, addOns);
        Assert.Equal(reported, problem is not null);
    }

    [Fact]
    public void The_internal_customer_is_the_bootstrap_one_with_a_service_head_office()
    {
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), InternalCustomerRepository.CustomerId);
        Assert.Equal(InternalCustomerRepository.CustomerId, InternalCustomerRepository.OrgId);
        Assert.Equal("HO", InternalCustomerRepository.BranchCode);
        Assert.Equal(BusinessType.Service, InternalCustomerRepository.BranchBusinessType);
    }

    /// <summary>An older install's bootstrap customer: Standard, a ten-year licence, a Retail head office.</summary>
    private static async Task<(Guid CustomerId, Guid HeadOfficeId)> SeedOldBootstrapAsync(AdminDbContext db)
    {
        Guid customerId = Guid.NewGuid();
        Guid headOffice = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        string suffix = customerId.ToString("N")[..8];

        db.Customers.Add(new Master.Entity.TableEntities.Customer
        {
            CustomerId = customerId,
            CustomerCode = Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString(),
            CountryPrefix = "IN",
            Name = $"Old Bootstrap {suffix}",
            BillingEmail = $"old-{suffix}@example.com",
            Status = TenantStatus.Expired,
            PlanTier = PlanTier.Elite,
            DatabaseName = "IN000001",
        });
        db.Licenses.Add(new License
        {
            CustomerId = customerId,
            LicenseType = LicenseType.Elite,
            StartDate = new DateOnly(2016, 1, 1),
            ExpiryDate = new DateOnly(2026, 1, 1),
            MaxUsers = 100,
            MaxOrganizations = 100,
            IsActive = true,
        });
        foreach ((Guid id, string code) in new[] { (headOffice, "HO"), (second, "B2") })
        {
            db.Organizations.Add(new Organization
            {
                OrgId = id,
                CustomerId = customerId,
                OrgCode = $"{code}{suffix}",
                Name = code,
                BaseCurrency = "INR",
                FinancialYearStartMonth = 4,
                CountryId = 1,
                Status = TenantStatus.Active,
                BusinessType = BusinessType.Retail,
                Vertical = Vertical.Retail,
                ExpiryDate = new DateOnly(2026, 1, 1),
            });
        }

        await db.SaveChangesAsync();
        return (customerId, headOffice);
    }

    [SkippableFact]
    public async Task An_older_bootstrap_customer_becomes_internal_once()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, Guid headOffice) = await SeedOldBootstrapAsync(db);
        var repository = new InternalCustomerRepository(db);

        Assert.True(await repository.ConvertAsync(customerId, headOffice, AddOn.Hrms, default));

        db.ChangeTracker.Clear();
        Master.Entity.TableEntities.Customer customer = await db.Customers.SingleAsync(c => c.CustomerId == customerId);
        License licence = await db.Licenses.SingleAsync(l => l.CustomerId == customerId);
        Organization ho = await db.Organizations.SingleAsync(o => o.OrgId == headOffice);
        Organization other = await db.Organizations.SingleAsync(o => o.CustomerId == customerId && o.OrgId != headOffice);

        Assert.Equal(CustomerKind.Internal, customer.Kind);
        Assert.Equal(TenantStatus.Active, customer.Status);
        Assert.Equal(CustomerKindRepository.NoExpiry, licence.ExpiryDate);
        Assert.Equal(CustomerKindRepository.UnbilledMaxUsers, licence.MaxUsers);
        Assert.Equal(BusinessType.Service, ho.BusinessType);
        Assert.Equal(Vertical.Service, ho.Vertical);
        Assert.Equal(AddOn.Hrms, ho.AddOns);
        Assert.Null(ho.ExpiryDate);

        // Only the head office changes trade; every branch loses its own expiry.
        Assert.Equal(BusinessType.Retail, other.BusinessType);
        Assert.Null(other.ExpiryDate);
    }

    [SkippableFact]
    public async Task A_second_start_changes_nothing_not_even_a_branch_the_operator_changed()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, Guid headOffice) = await SeedOldBootstrapAsync(db);
        var repository = new InternalCustomerRepository(db);
        await repository.ConvertAsync(customerId, headOffice, AddOn.None, default);

        // The owner later makes the head office a Pharma branch.
        await db.Organizations.Where(o => o.OrgId == headOffice)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.BusinessType, BusinessType.Pharma));

        Assert.False(await repository.ConvertAsync(customerId, headOffice, AddOn.Payroll, default));

        db.ChangeTracker.Clear();
        Organization ho = await db.Organizations.SingleAsync(o => o.OrgId == headOffice);
        Assert.Equal(BusinessType.Pharma, ho.BusinessType);
        Assert.Equal(AddOn.None, ho.AddOns);
        Assert.Equal(1, await db.Customers.CountAsync(c => c.CustomerId == customerId));
    }

    [SkippableFact]
    public async Task No_bootstrap_customer_means_nothing_to_convert()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();

        Assert.False(await new InternalCustomerRepository(db).ConvertAsync(Guid.NewGuid(), Guid.NewGuid(), AddOn.None, default));
    }
}
