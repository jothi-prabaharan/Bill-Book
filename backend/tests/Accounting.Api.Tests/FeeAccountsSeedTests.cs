using Accounting.Entity.Enums;
using Accounting.Entity.TableEntities;
using Accounting.Repository.SeedData;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// The accounts School fees post to (TK-64): in every branch's chart, with the
/// types and contra flag a report depends on. No database needed.
/// </summary>
public sealed class FeeAccountsSeedTests
{
    private static readonly IReadOnlyList<Account> Chart = ChartOfAccountsSeed.Build(Guid.NewGuid());

    private static Account Named(SystemAccount account) =>
        Chart.Single(a => a.AccountSystemName == SystemAccountNames.Of(account));

    [Fact]
    public void Fee_income_is_income_and_not_contra() =>
        Assert.Equal((4, false), (Named(SystemAccount.FeeIncome).AccountTypeId, Named(SystemAccount.FeeIncome).IsContra));

    [Fact]
    public void Discount_given_is_a_contra_income_so_reports_subtract_it() =>
        Assert.Equal((4, true), (Named(SystemAccount.DiscountGiven).AccountTypeId, Named(SystemAccount.DiscountGiven).IsContra));

    [Fact]
    public void Refundable_deposits_are_a_liability() =>
        Assert.Equal(2, Named(SystemAccount.RefundableDeposits).AccountTypeId);

    [Fact]
    public void Every_seeded_account_has_its_own_code_and_name()
    {
        Assert.Equal(Chart.Count, Chart.Select(a => a.AccountCode).Distinct().Count());
        Assert.Equal(Chart.Count, Chart.Select(a => a.AccountSystemName).Distinct().Count());
    }
}
