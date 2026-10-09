using Shared.Kernel.Apps;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// Unit tests for <see cref="BusinessType"/>, <see cref="AddOn"/>, and <see cref="AddOnRules"/> (D-36, TK-126).
/// Tests are written by AI and run by the repository owner (Section 0.5 of docs/TASKS.md).
/// </summary>
public sealed class AddOnRulesTests
{
    [Fact]
    public void Null_business_type_allows_only_hrms_and_payroll()
    {
        AddOn allowed = AddOnRules.Allowed(null);

        Assert.Equal(AddOn.Hrms | AddOn.Payroll, allowed);
        Assert.True(AddOnRules.IsAllowed(null, AddOn.Hrms));
        Assert.True(AddOnRules.IsAllowed(null, AddOn.Payroll));
        Assert.True(AddOnRules.IsAllowed(null, AddOn.Hrms | AddOn.Payroll));
        Assert.True(AddOnRules.IsAllowed(null, AddOn.None));

        // Retail and general add-ons are not allowed when business type is null
        Assert.False(AddOnRules.IsAllowed(null, AddOn.Crm));
        Assert.False(AddOnRules.IsAllowed(null, AddOn.Pos));
        Assert.False(AddOnRules.IsAllowed(null, AddOn.FixedAssets));
        Assert.False(AddOnRules.IsAllowed(null, AddOn.ClientPortal));
        Assert.False(AddOnRules.IsAllowed(null, AddOn.EInvoicing));
        Assert.False(AddOnRules.IsAllowed(null, AddOn.Budget));
        Assert.False(AddOnRules.IsAllowed(null, AddOn.RecurringBilling));
        Assert.False(AddOnRules.IsAllowed(null, AddOn.All));
    }

    [Fact]
    public void School_allows_hrms_payroll_crm_fixedassets_clientportal_budget_recurringbilling_but_not_pos_or_einvoicing()
    {
        AddOn allowed = AddOnRules.Allowed(BusinessType.School);

        // Positive assertions
        Assert.True(AddOnRules.IsAllowed(BusinessType.School, AddOn.Hrms));
        Assert.True(AddOnRules.IsAllowed(BusinessType.School, AddOn.Payroll));
        Assert.True(AddOnRules.IsAllowed(BusinessType.School, AddOn.Crm));
        Assert.True(AddOnRules.IsAllowed(BusinessType.School, AddOn.FixedAssets));
        Assert.True(AddOnRules.IsAllowed(BusinessType.School, AddOn.ClientPortal));
        Assert.True(AddOnRules.IsAllowed(BusinessType.School, AddOn.Budget));
        Assert.True(AddOnRules.IsAllowed(BusinessType.School, AddOn.RecurringBilling));

        // Negative assertions (Pos and EInvoicing are only for General, Pharma, Jewellery)
        Assert.False(AddOnRules.IsAllowed(BusinessType.School, AddOn.Pos));
        Assert.False(AddOnRules.IsAllowed(BusinessType.School, AddOn.EInvoicing));
        Assert.Equal(0, (int)(allowed & AddOn.Pos));
        Assert.Equal(0, (int)(allowed & AddOn.EInvoicing));

        // Combined flags
        Assert.True(AddOnRules.IsAllowed(BusinessType.School, AddOn.Hrms | AddOn.Crm | AddOn.Budget));
        Assert.False(AddOnRules.IsAllowed(BusinessType.School, AddOn.Crm | AddOn.Pos));
        Assert.False(AddOnRules.IsAllowed(BusinessType.School, AddOn.All));
    }

    [Theory]
    [InlineData(BusinessType.Retail)]
    [InlineData(BusinessType.Pharma)]
    public void Retail_and_pharma_allow_every_addon_but_the_savings_scheme_and_site_services(BusinessType businessType)
    {
        Assert.Equal(AddOn.All & ~AddOn.SavingsScheme & ~AddOn.SiteServices, AddOnRules.Allowed(businessType));
        Assert.True(AddOnRules.IsAllowed(businessType, AddOn.Pos));
        Assert.True(AddOnRules.IsAllowed(businessType, AddOn.EInvoicing));
        Assert.True(AddOnRules.IsAllowed(businessType, AddOn.RecurringBilling));
        Assert.False(AddOnRules.IsAllowed(businessType, AddOn.SavingsScheme));
        Assert.False(AddOnRules.IsAllowed(businessType, AddOn.SiteServices));
    }

    [Fact]
    public void Jewellery_allows_every_addon_except_site_services()
    {
        Assert.Equal(AddOn.All & ~AddOn.SiteServices, AddOnRules.Allowed(BusinessType.Jewellery));
        Assert.True(AddOnRules.IsAllowed(BusinessType.Jewellery, AddOn.SavingsScheme));
        Assert.False(AddOnRules.IsAllowed(BusinessType.Jewellery, AddOn.SiteServices));
    }

    [Fact]
    public void Restaurant_allows_every_addon_but_pos_savings_scheme_and_site_services()
    {
        // D-43: a restaurant bills through its own order screen, so the retail till is not offered.
        Assert.Equal(AddOn.All & ~AddOn.SavingsScheme & ~AddOn.Pos & ~AddOn.SiteServices, AddOnRules.Allowed(BusinessType.Restaurant));
        Assert.False(AddOnRules.IsAllowed(BusinessType.Restaurant, AddOn.Pos));
        Assert.False(AddOnRules.IsAllowed(BusinessType.Restaurant, AddOn.SiteServices));
        Assert.True(AddOnRules.IsAllowed(BusinessType.Restaurant, AddOn.Crm));
        Assert.True(AddOnRules.IsAllowed(BusinessType.Restaurant, AddOn.RecurringBilling));
        Assert.True(AddOnRules.IsDefined(BusinessType.Restaurant));
    }

    [Fact]
    public void Service_allows_site_services_addon()
    {
        AddOn allowed = AddOnRules.Allowed(BusinessType.Service);
        Assert.True(AddOnRules.IsAllowed(BusinessType.Service, AddOn.SiteServices));
        Assert.False(AddOnRules.IsAllowed(BusinessType.Service, AddOn.Pos));
        Assert.False(AddOnRules.IsAllowed(BusinessType.Service, AddOn.EInvoicing));
        Assert.False(AddOnRules.IsAllowed(BusinessType.Service, AddOn.SavingsScheme));
        Assert.False(AddOnRules.IsAllowed(BusinessType.Retail, AddOn.SiteServices));
        Assert.False(AddOnRules.IsAllowed(BusinessType.Pharma, AddOn.SiteServices));
        Assert.False(AddOnRules.IsAllowed(BusinessType.Jewellery, AddOn.SiteServices));
        Assert.False(AddOnRules.IsAllowed(BusinessType.School, AddOn.SiteServices));
        Assert.False(AddOnRules.IsAllowed(BusinessType.Restaurant, AddOn.SiteServices));
        Assert.False(AddOnRules.IsAllowed(null, AddOn.SiteServices));
    }

    [Fact]
    public void Undefined_business_type_allows_none()
    {
        var invalidType = (BusinessType)99;
        Assert.Equal(AddOn.None, AddOnRules.Allowed(invalidType));
        Assert.False(AddOnRules.IsAllowed(invalidType, AddOn.Hrms));
        Assert.False(AddOnRules.IsAllowed(invalidType, AddOn.Pos));
    }

    [Theory]
    [InlineData(null, true, null)]
    [InlineData("", true, null)]
    [InlineData("   ", true, null)]
    [InlineData("General", true, BusinessType.Retail)]
    [InlineData("general", true, BusinessType.Retail)]
    [InlineData("Pharma", true, BusinessType.Pharma)]
    [InlineData("pharma", true, BusinessType.Pharma)]
    [InlineData("Jewellery", true, BusinessType.Jewellery)]
    [InlineData("jewellery", true, BusinessType.Jewellery)]
    [InlineData("School", true, BusinessType.School)]
    [InlineData("school", true, BusinessType.School)]
    [InlineData("Restaurant", true, BusinessType.Restaurant)]
    [InlineData("restaurant", true, BusinessType.Restaurant)]
    [InlineData("Invalid", false, null)]
    [InlineData("1", false, null)]
    public void TryParseBusinessType_parses_expected_values(string? input, bool expectedSuccess, BusinessType? expectedType)
    {
        bool success = AddOnRules.TryParseBusinessType(input, out BusinessType? actual);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedType, actual);
    }

    [Fact]
    public void TryParseAddOn_parses_valid_and_invalid_inputs()
    {
        Assert.True(AddOnRules.TryParseAddOn(null, out AddOn empty));
        Assert.Equal(AddOn.None, empty);

        Assert.True(AddOnRules.TryParseAddOn("Hrms", out AddOn hrms));
        Assert.Equal(AddOn.Hrms, hrms);

        Assert.True(AddOnRules.TryParseAddOn("Hrms, Payroll", out AddOn combined));
        Assert.Equal(AddOn.Hrms | AddOn.Payroll, combined);

        Assert.True(AddOnRules.TryParseAddOn("3", out AddOn numeric));
        Assert.Equal(AddOn.Hrms | AddOn.Payroll, numeric);

        Assert.False(AddOnRules.TryParseAddOn("NotAnAddOn", out _));
        Assert.False(AddOnRules.TryParseAddOn("99999", out _));
    }

    [Fact]
    public void Split_enumerates_individual_flags()
    {
        Assert.Empty(AddOnRules.Split(AddOn.None));

        List<AddOn> single = AddOnRules.Split(AddOn.Hrms).ToList();
        Assert.Single(single);
        Assert.Equal(AddOn.Hrms, single[0]);

        List<AddOn> multiple = AddOnRules.Split(AddOn.Hrms | AddOn.Pos | AddOn.Budget).ToList();
        Assert.Equal(3, multiple.Count);
        Assert.Contains(AddOn.Hrms, multiple);
        Assert.Contains(AddOn.Pos, multiple);
        Assert.Contains(AddOn.Budget, multiple);

        List<AddOn> all = AddOnRules.Split(AddOn.All).ToList();
        Assert.Equal(11, all.Count);
        Assert.DoesNotContain(AddOn.None, all);
        Assert.DoesNotContain(AddOn.All, all);
    }

    [Theory]
    [InlineData("Retail")]
    [InlineData("General")] // a token or page from before TK-136
    [InlineData(" general ")]
    public void Retail_and_its_old_name_both_parse_as_retail(string value)
    {
        Assert.True(AddOnRules.TryParseBusinessType(value, out BusinessType? parsed));
        Assert.Equal(BusinessType.Retail, parsed);
    }

    [Fact]
    public void A_token_minted_before_the_rename_still_names_a_retail_branch()
    {
        var user = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(Shared.Kernel.Internal.ScopeClaims.BusinessTypeClaim, "General")], "test"));

        Assert.Equal(BusinessType.Retail, Shared.Kernel.Internal.ScopeClaims.BusinessTypeOf(user));
    }
}
