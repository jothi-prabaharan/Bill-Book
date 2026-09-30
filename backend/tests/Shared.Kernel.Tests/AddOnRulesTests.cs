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
    [InlineData(BusinessType.General)]
    [InlineData(BusinessType.Pharma)]
    [InlineData(BusinessType.Jewellery)]
    public void General_pharma_and_jewellery_allow_all_addons(BusinessType businessType)
    {
        AddOn allowed = AddOnRules.Allowed(businessType);

        Assert.Equal(AddOn.All, allowed);
        Assert.True(AddOnRules.IsAllowed(businessType, AddOn.Hrms));
        Assert.True(AddOnRules.IsAllowed(businessType, AddOn.Payroll));
        Assert.True(AddOnRules.IsAllowed(businessType, AddOn.Crm));
        Assert.True(AddOnRules.IsAllowed(businessType, AddOn.Pos));
        Assert.True(AddOnRules.IsAllowed(businessType, AddOn.FixedAssets));
        Assert.True(AddOnRules.IsAllowed(businessType, AddOn.ClientPortal));
        Assert.True(AddOnRules.IsAllowed(businessType, AddOn.EInvoicing));
        Assert.True(AddOnRules.IsAllowed(businessType, AddOn.Budget));
        Assert.True(AddOnRules.IsAllowed(businessType, AddOn.RecurringBilling));
        Assert.True(AddOnRules.IsAllowed(businessType, AddOn.All));
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
    [InlineData("General", true, BusinessType.General)]
    [InlineData("general", true, BusinessType.General)]
    [InlineData("Pharma", true, BusinessType.Pharma)]
    [InlineData("pharma", true, BusinessType.Pharma)]
    [InlineData("Jewellery", true, BusinessType.Jewellery)]
    [InlineData("jewellery", true, BusinessType.Jewellery)]
    [InlineData("School", true, BusinessType.School)]
    [InlineData("school", true, BusinessType.School)]
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
        Assert.Equal(9, all.Count);
        Assert.DoesNotContain(AddOn.None, all);
        Assert.DoesNotContain(AddOn.All, all);
    }
}
