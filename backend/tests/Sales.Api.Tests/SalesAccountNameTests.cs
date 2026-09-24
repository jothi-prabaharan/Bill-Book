using System.Reflection;
using Accounting.Entity.Enums;
using Accounting.Repository.SeedData;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// Every account Sales posts to, held to the chart Accounting actually seeds.
///
/// <b>Why this exists.</b> The ledger finds an account by its exact system name.
/// The invoice posted to "Sales" and "Tax Payable" and the credit note to
/// "Sales Returns" — names that were in no chart — so in a real deployment
/// every invoice and every credit note was refused. Nothing noticed, because
/// every Sales test posts to a stub ledger that accepts any name at all
/// (TK-77).
///
/// The names are read off the services themselves — every <c>const string</c>
/// whose name ends in <c>Account</c> — so a new posting site is covered the
/// moment it declares its account, without anybody remembering to list it.
/// </summary>
public sealed class SalesAccountNameTests
{
    /// <summary>
    /// Named on purpose, each with the card that seeds it. Adding to this list
    /// is how a new unseeded name gets past the test, and it should be as
    /// uncomfortable as that sounds.
    /// </summary>
    private static readonly Dictionary<string, string> NotSeededYet = new()
    {
        ["Goods Delivered Not Invoiced"] = "TK-90 (split from TK-10): the invoice against a challan clears it",
        ["Cash"] = "POS (TK-39): a till sale's cash account is a child of Cash in Hand, which is locked",
    };

    public static TheoryData<string, string> PostedAccounts()
    {
        var data = new TheoryData<string, string>();

        foreach (Type type in typeof(Sales.Api.Services.InvoiceService).Assembly.GetTypes()
            .Where(t => t.Namespace == "Sales.Api.Services"))
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name.EndsWith("Account", StringComparison.Ordinal)))
            {
                data.Add(type.Name, (string)field.GetRawConstantValue()!);
            }
        }

        return data;
    }

    [Fact]
    public void The_services_declare_the_accounts_they_post_to()
    {
        // Guards the theory below: if reflection found nothing, it would pass
        // over an empty set and prove the opposite of what it claims.
        Assert.Contains(PostedAccounts(), row => (string)row[0] == "InvoiceService");
        Assert.Contains(PostedAccounts(), row => (string)row[0] == "CreditNoteService");
    }

    [Theory]
    [MemberData(nameof(PostedAccounts))]
    public void Every_account_a_sales_document_posts_to_is_in_the_seeded_chart(string service, string account)
    {
        if (NotSeededYet.ContainsKey(account))
        {
            return;
        }

        HashSet<string?> seeded = ChartOfAccountsSeed.Build(Guid.NewGuid())
            .Select(a => a.AccountSystemName)
            .ToHashSet();

        Assert.True(
            seeded.Contains(account),
            $"{service} posts to '{account}', which Accounting's chart of accounts does not seed. "
                + $"The seeded names are: {string.Join(", ", seeded.Order())}.");
    }

    [Fact]
    public void The_exceptions_are_still_unseeded()
    {
        // When TK-10 seeds one of these, this fails and the exception comes off
        // the list rather than lingering as a hole nobody remembers.
        HashSet<string?> seeded = ChartOfAccountsSeed.Build(Guid.NewGuid())
            .Select(a => a.AccountSystemName)
            .ToHashSet();

        Assert.All(NotSeededYet.Keys, name => Assert.DoesNotContain(name, seeded));
    }

    [Fact]
    public void Sales_returns_is_a_contra_income_account()
    {
        var returns = ChartOfAccountsSeed.Build(Guid.NewGuid())
            .Single(a => a.AccountSystemName == SystemAccountNames.Of(SystemAccount.SalesReturns));

        // A report subtracts it from sales only if it is marked contra. Miss it
        // and returns are added to revenue.
        Assert.True(returns.IsContra);
        Assert.Equal(4, returns.AccountTypeId); // Income
    }
}
