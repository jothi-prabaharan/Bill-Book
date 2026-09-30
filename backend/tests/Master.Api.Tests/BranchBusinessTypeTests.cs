using System.IdentityModel.Tokens.Jwt;
using Master.Api.Services;
using Master.Entity.Models;
using Master.Repository.Clients;
using Master.Repository.Repositories;
using Microsoft.Extensions.Options;
using Shared.Kernel.Apps;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Verifies branch business type, add-ons, licence decoupling, token claims,
/// tenant seeding, and menu gating (D-36, TK-127).
/// </summary>
public sealed class BranchBusinessTypeTests
{
    [Fact]
    public void Branch_validation_requires_business_type_or_addon()
    {
        // Neither business type nor add-on is invalid
        Assert.False(IsValidBranch(null, AddOn.None));

        // Standalone add-ons are valid
        Assert.True(IsValidBranch(null, AddOn.Hrms));
        Assert.True(IsValidBranch(null, AddOn.Payroll));

        // Business types without add-ons are valid
        Assert.True(IsValidBranch(BusinessType.General, AddOn.None));
        Assert.True(IsValidBranch(BusinessType.Pharma, AddOn.None));
        Assert.True(IsValidBranch(BusinessType.Jewellery, AddOn.None));
        Assert.True(IsValidBranch(BusinessType.School, AddOn.None));
    }

    [Fact]
    public void Branch_validation_refuses_disallowed_addons()
    {
        // School does not allow POS or EInvoicing
        Assert.False(AddOnRules.IsAllowed(BusinessType.School, AddOn.Pos));
        Assert.False(AddOnRules.IsAllowed(BusinessType.School, AddOn.EInvoicing));

        // School allows HRMS, Payroll, CRM, etc.
        Assert.True(AddOnRules.IsAllowed(BusinessType.School, AddOn.Hrms));
        Assert.True(AddOnRules.IsAllowed(BusinessType.School, AddOn.Payroll));
        Assert.True(AddOnRules.IsAllowed(BusinessType.School, AddOn.Crm));

        // Retail verticals allow POS and EInvoicing
        Assert.True(AddOnRules.IsAllowed(BusinessType.General, AddOn.Pos));
        Assert.True(AddOnRules.IsAllowed(BusinessType.Pharma, AddOn.EInvoicing));
        Assert.True(AddOnRules.IsAllowed(BusinessType.Jewellery, AddOn.Pos | AddOn.EInvoicing));
    }

    [Fact]
    public void Access_token_carries_business_type_and_addons_claims()
    {
        var options = Options.Create(new JwtOptionsRepository
        {
            Issuer = "bill-book",
            Audience = "bill-book",
            SigningKey = "a-test-signing-key-that-is-long-enough-for-hmac-sha256",
            AccessTokenMinutes = 15,
        });

        var repository = new JwtTokenRepository(options, TimeProvider.System);

        var tokenString = repository.CreateAccessToken(new AccessTokenRequest
        {
            UserId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerCode = "CUST01",
            OrgId = Guid.NewGuid(),
            DisplayName = "Test User",
            LicenseStatus = "Active",
            Vertical = "General",
            RoleId = 1,
            BusinessType = BusinessType.General,
            AddOns = AddOn.Hrms | AddOn.Payroll,
            Permissions = ["sales.view", "accounting.view"],
        });

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokenString);

        Assert.Equal("General", jwt.Claims.FirstOrDefault(c => c.Type == "business_type")?.Value);
        Assert.Equal(((int)(AddOn.Hrms | AddOn.Payroll)).ToString(), jwt.Claims.FirstOrDefault(c => c.Type == "add_ons")?.Value);
    }

    [Fact]
    public void Standalone_hrms_token_omits_business_type_and_sets_addons()
    {
        var options = Options.Create(new JwtOptionsRepository
        {
            Issuer = "bill-book",
            Audience = "bill-book",
            SigningKey = "a-test-signing-key-that-is-long-enough-for-hmac-sha256",
            AccessTokenMinutes = 15,
        });

        var repository = new JwtTokenRepository(options, TimeProvider.System);

        var tokenString = repository.CreateAccessToken(new AccessTokenRequest
        {
            UserId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerCode = "CUST01",
            OrgId = Guid.NewGuid(),
            DisplayName = "HR Manager",
            LicenseStatus = "Active",
            Vertical = "General",
            RoleId = 1,
            BusinessType = null,
            AddOns = AddOn.Hrms,
            Permissions = ["employee.view"],
        });

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokenString);

        Assert.Null(jwt.Claims.FirstOrDefault(c => c.Type == "business_type"));
        Assert.Equal(((int)AddOn.Hrms).ToString(), jwt.Claims.FirstOrDefault(c => c.Type == "add_ons")?.Value);
        Assert.Equal("Hrms", jwt.Claims.FirstOrDefault(c => c.Type == "app")?.Value);
    }

    [Fact]
    public void Standalone_hrms_branch_seeds_only_hrms_services_and_no_contacts()
    {
        IReadOnlyList<string> services = HttpTenantSeeder.ServicesFor(null, AddOn.Hrms);

        Assert.Equal(["Accounting", "Employee", "TimeLeave", "Printing"], services);
        Assert.False(HttpTenantSeeder.SeedsContacts(null, AddOn.Hrms));
    }

    [Fact]
    public void Retail_branch_seeds_all_retail_services_and_contacts()
    {
        IReadOnlyList<string> services = HttpTenantSeeder.ServicesFor(BusinessType.General, AddOn.None);

        Assert.Equal(["Accounting", "Inventory", "Sales", "Purchase", "Reporting", "Printing", "Customer"], services);
        Assert.True(HttpTenantSeeder.SeedsContacts(BusinessType.General, AddOn.None));
    }

    [Fact]
    public void School_branch_seeds_school_services_and_contacts()
    {
        IReadOnlyList<string> services = HttpTenantSeeder.ServicesFor(BusinessType.School, AddOn.None);

        Assert.Contains("Accounting", services);
        Assert.Contains("Employee", services);
        Assert.Contains("Student", services);
        Assert.Contains("Fee", services);
        Assert.Contains("Printing", services);
        Assert.DoesNotContain("Inventory", services);
        Assert.DoesNotContain("Sales", services);
        Assert.True(HttpTenantSeeder.SeedsContacts(BusinessType.School, AddOn.None));
    }

    [Fact]
    public void Standalone_hrms_menu_matches_only_hrms_and_shared_screens()
    {
        // Shared home / settings (null business type, null/none add-on) -> matches
        Assert.True(MenuRepository.MenuMatches(null, AddOn.Hrms, null, AddOn.None));

        // HRMS screens -> matches
        Assert.True(MenuRepository.MenuMatches(null, AddOn.Hrms, null, AddOn.Hrms));

        // Retail trading screens -> does NOT match
        Assert.False(MenuRepository.MenuMatches(null, AddOn.Hrms, BusinessType.General, AddOn.None));

        // School screens -> does NOT match
        Assert.False(MenuRepository.MenuMatches(null, AddOn.Hrms, BusinessType.School, AddOn.None));

        // Contacts screen (needs business type or CRM add-on) -> does NOT match
        Assert.False(MenuRepository.MenuMatches(null, AddOn.Hrms, BusinessType.General, AddOn.Crm));
    }

    [Fact]
    public void Retail_branch_menu_matches_retail_and_shared_screens_but_not_hrms()
    {
        // Shared home / settings -> matches
        Assert.True(MenuRepository.MenuMatches(BusinessType.General, AddOn.None, null, AddOn.None));

        // Retail screens -> matches
        Assert.True(MenuRepository.MenuMatches(BusinessType.General, AddOn.None, BusinessType.General, AddOn.None));

        // Contacts screen -> matches
        Assert.True(MenuRepository.MenuMatches(BusinessType.General, AddOn.None, BusinessType.General, AddOn.Crm));

        // HRMS screen -> does NOT match
        Assert.False(MenuRepository.MenuMatches(BusinessType.General, AddOn.None, null, AddOn.Hrms));

        // School screen -> does NOT match
        Assert.False(MenuRepository.MenuMatches(BusinessType.General, AddOn.None, BusinessType.School, AddOn.None));
    }

    private static bool IsValidBranch(BusinessType? businessType, AddOn addOns) =>
        (businessType is not null || addOns != AddOn.None) && AddOnRules.IsAllowed(businessType, addOns);
}
