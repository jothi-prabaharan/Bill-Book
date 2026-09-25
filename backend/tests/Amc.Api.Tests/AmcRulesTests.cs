using Amc.Api.Services;
using Amc.Entity.Enums;
using Xunit;

namespace Amc.Api.Tests;

/// <summary>A contract's rules (S8, TK-68). Pure, so they need no database.</summary>
public sealed class AmcRulesTests
{
    private static readonly DateOnly End = new(2027, 3, 31);

    [Fact]
    public void An_active_contract_past_its_end_date_shows_as_expired()
    {
        Assert.Equal(ContractStatus.Expired, AmcRules.Effective(ContractStatus.Active, End, End.AddDays(1)));
        Assert.Equal(ContractStatus.Active, AmcRules.Effective(ContractStatus.Active, End, End));
        Assert.Equal(ContractStatus.Terminated, AmcRules.Effective(ContractStatus.Terminated, End, End.AddDays(1)));
    }

    [Theory]
    [InlineData(ContractStatus.Active, 30, "2027-03-01", true)]
    [InlineData(ContractStatus.Active, 30, "2027-02-28", false)]
    [InlineData(ContractStatus.Active, 30, "2027-03-31", true)]
    [InlineData(ContractStatus.Active, 30, "2027-04-01", false)]
    [InlineData(ContractStatus.Active, 0, "2027-03-31", false)]
    [InlineData(ContractStatus.Draft, 30, "2027-03-15", false)]
    [InlineData(ContractStatus.Terminated, 30, "2027-03-15", false)]
    public void A_renewal_reminder_is_due_in_the_window_before_the_end(ContractStatus status, int days, string today, bool expected) =>
        Assert.Equal(expected, AmcRules.IsRenewalDue(status, End, days, DateOnly.Parse(today)));

    [Fact]
    public void Terms_overlap_unless_one_ends_before_the_other_starts()
    {
        var a = (new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31));
        Assert.True(AmcRules.Overlaps(a.Item1, a.Item2, new DateOnly(2027, 3, 31), new DateOnly(2028, 3, 30)));
        Assert.False(AmcRules.Overlaps(a.Item1, a.Item2, new DateOnly(2027, 4, 1), new DateOnly(2028, 3, 31)));
    }

    [Fact]
    public void Only_a_draft_changes_its_terms_and_only_draft_or_active_take_changes()
    {
        Assert.Equal([ContractStatus.Draft], Enum.GetValues<ContractStatus>().Where(AmcRules.TermsEditable));
        Assert.Equal([ContractStatus.Draft, ContractStatus.Active], Enum.GetValues<ContractStatus>().Where(AmcRules.TakesChanges));
    }
}
