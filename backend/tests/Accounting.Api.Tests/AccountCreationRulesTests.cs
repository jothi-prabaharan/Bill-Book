using Accounting.Entity.Models;
using Accounting.Repository;
using Accounting.Repository.Clients;
using Accounting.Repository.Repositories;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// The rules on an account's currency and bank flag, which the account screen and the API both rely on:
/// a currency is one the branch uses, and the bank flag belongs to Banking, so neither a create nor an
/// update may set or clear it. Written by an AI and not run (section 0.5 of docs/Task_Details.md).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AccountCreationRulesTests
{
    private readonly PostgresFixture _postgres;

    public AccountCreationRulesTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>A branch whose currency list cannot be read.</summary>
    private sealed class UnreadableBranchCurrencies : IBranchCurrencies
    {
        public Task<IReadOnlyList<string>?> ActiveCodesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>?>(null);
    }

    private static SaveAccountRequest Request(string code, string? currency = null, bool isBank = false) => new()
    {
        AccountTypeId = 1,
        AccountCode = code,
        AccountName = "Rule check " + code,
        CurrencyCode = currency,
        IsBank = isBank,
        IsActive = true,
    };

    [SkippableFact]
    public async Task A_currency_the_branch_does_not_use_is_refused()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var (db, _) = Open();
        var accounts = new AccountRepository(db, new StubBranchCurrencies("INR", "USD"));

        SaveAccountResult result = await accounts.CreateAsync(Request(Code(), currency: "ZZZ"), default);

        Assert.Equal(SaveAccountOutcome.CurrencyNotInUse, result.Outcome);
        Assert.Null(result.AccountId);
    }

    [SkippableFact]
    public async Task A_currency_the_branch_uses_is_accepted()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var (db, _) = Open();
        var accounts = new AccountRepository(db, new StubBranchCurrencies("INR", "USD"));

        SaveAccountResult result = await accounts.CreateAsync(Request(Code(), currency: "USD"), default);

        Assert.Equal(SaveAccountOutcome.Ok, result.Outcome);
        Assert.NotNull(result.AccountId);
    }

    [SkippableFact]
    public async Task A_currency_is_refused_when_the_branch_list_cannot_be_read()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var (db, _) = Open();
        var accounts = new AccountRepository(db, new UnreadableBranchCurrencies());

        SaveAccountResult result = await accounts.CreateAsync(Request(Code(), currency: "INR"), default);

        Assert.Equal(SaveAccountOutcome.CurrencyUnavailable, result.Outcome);
    }

    [SkippableFact]
    public async Task The_bank_flag_cannot_be_set_on_create()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var (db, _) = Open();
        var accounts = new AccountRepository(db, new StubBranchCurrencies("INR"));

        SaveAccountResult result = await accounts.CreateAsync(Request(Code(), isBank: true), default);

        Assert.Equal(SaveAccountOutcome.BankFlagNotAllowed, result.Outcome);
    }

    [SkippableFact]
    public async Task The_bank_flag_cannot_be_changed_on_update()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var (db, _) = Open();
        var accounts = new AccountRepository(db, new StubBranchCurrencies("INR"));
        SaveAccountResult created = await accounts.CreateAsync(Request(Code()), default);

        SaveAccountResult result = await accounts.UpdateAsync(created.AccountId!.Value, Request(Code(), isBank: true), default);

        Assert.Equal(SaveAccountOutcome.BankFlagNotAllowed, result.Outcome);
    }

    private static string Code() => "R" + Guid.NewGuid().ToString("N")[..10];

    private (AccountingDbContext Db, Guid OrgId) Open()
    {
        var customerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        return (_postgres.CreateContext(customerId, orgId), orgId);
    }
}
