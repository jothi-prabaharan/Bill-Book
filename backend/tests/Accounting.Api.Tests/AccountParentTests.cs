using Accounting.Api.Services;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Accounting.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// Verifies the recursive account and subaccount rules:
/// 1. Hierarchy & parent selection must match the same AccountTypeId.
/// 2. Used accounts with transactions cannot become parent accounts.
/// 3. Parent accounts cannot accept transactions across any module.
/// 4. Cycles and self-parenting are strictly rejected.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AccountParentTests
{
    private readonly PostgresFixture _postgres;

    public AccountParentTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed class StubInventory : IInventoryOpeningStock
    {
        public Task<OpeningStockOutcome> RecordAsync(
            long openingBalanceId,
            DateOnly asOfDate,
            IReadOnlyList<OpeningStockRequestLine> lines,
            CancellationToken ct) =>
            Task.FromResult(new OpeningStockOutcome(true, 0m));
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required AccountingDbContext Db { get; init; }
        public required AccountRepository Accounts { get; init; }
        public required DocumentChoicesRepository Choices { get; init; }
        public required JournalRepository Journals { get; init; }
        public required OpeningBalanceRepository OpeningBalances { get; init; }
        public required LedgerPostingRepository Postings { get; init; }
        public required Guid OrgId { get; init; }

        public static async Task<Harness> CreateAsync(PostgresFixture postgres)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var orgId = Guid.NewGuid();
            var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = orgId };
            AccountingDbContext db = postgres.CreateContext(tenant.CustomerId!.Value, tenant.OrgId!.Value);

            var accounts = new AccountRepository(db);
            var numbers = new NumberGenerator(
                db,
                Options.Create(new NumberingOptions()),
                new StubFinancialYear());
            var choices = new DocumentChoicesRepository(db, numbers, TimeProvider.System);
            var postings = new LedgerPostingRepository(db, tenant, new StubBaseCurrency());
            var reports = new LedgerReportRepository(db);
            var journals = new JournalRepository(
                db,
                postings,
                new PeriodLockRepository(db, new StubCurrentUser()),
                numbers,
                new StubBaseCurrency(),
                new StubCurrentUser(),
                tenant,
                TimeProvider.System);
            var openingBalances = new OpeningBalanceRepository(
                db,
                postings,
                reports,
                new StubInventory(),
                numbers,
                new StubBaseCurrency(),
                tenant,
                new StubCurrentUser(),
                TimeProvider.System);

            return new Harness
            {
                Db = db,
                Accounts = accounts,
                Choices = choices,
                Journals = journals,
                OpeningBalances = openingBalances,
                Postings = postings,
                OrgId = orgId,
            };
        }

        public async Task<Account> CreateAccountAsync(string code, string name, int typeId, long? parentId = null)
        {
            var account = new Account
            {
                OrgId = OrgId,
                AccountTypeId = typeId,
                AccountCode = code,
                AccountName = name,
                ParentAccountId = parentId,
                IsActive = true,
                IsSales = true,
                IsPurchase = true,
                IsPayment = true,
            };
            Db.Accounts.Add(account);
            await Db.SaveChangesAsync();
            return account;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    [SkippableFact]
    public async Task Parent_must_share_the_exact_same_account_type()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        Account asset = await h.CreateAccountAsync("1001", "Asset Group", 1); // Asset = 1
        Account expense = await h.CreateAccountAsync("5001", "Office Supplies", 5); // Expense = 5

        // Trying to set Asset Group as parent of Office Supplies
        var request = new SaveAccountRequest
        {
            AccountTypeId = 5,
            AccountCode = "5001",
            AccountName = "Office Supplies",
            ParentAccountId = asset.AccountId,
            IsActive = true,
        };

        SaveAccountResult result = await h.Accounts.UpdateAsync(expense.AccountId, request, CancellationToken.None);
        Assert.Equal(SaveAccountOutcome.InvalidParent, result.Outcome);
    }

    [SkippableFact]
    public async Task Used_account_with_transactions_cannot_be_assigned_as_parent()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        Account usedAccount = await h.CreateAccountAsync("1002", "Checking Account", 1);

        // Add a ledger entry to simulate usage in transactions
        h.Db.JournalLedger.Add(new JournalLedger
        {
            OrgId = h.OrgId,
            AccountId = usedAccount.AccountId,
            TransactionTypeCode = "JRN",
            SourceDocumentId = 1,
            LedgerDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            DebitAmount = 100m,
            CreditAmount = 0m,
        });
        await h.Db.SaveChangesAsync();

        // Attempt to create a new child account under this used account
        var request = new SaveAccountRequest
        {
            AccountTypeId = 1,
            AccountCode = "1003",
            AccountName = "Sub Checking",
            ParentAccountId = usedAccount.AccountId,
            IsActive = true,
        };

        SaveAccountResult result = await h.Accounts.CreateAsync(request, CancellationToken.None);
        Assert.Equal(SaveAccountOutcome.ParentHasTransactions, result.Outcome);
    }

    [SkippableFact]
    public async Task Cycle_prevention_rejects_self_or_descendant_as_parent()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        Account parent = await h.CreateAccountAsync("1101", "Parent Account", 1);
        Account child = await h.CreateAccountAsync("1102", "Child Account", 1, parent.AccountId);

        // Self-parenting
        SaveAccountResult selfResult = await h.Accounts.UpdateAsync(parent.AccountId, new SaveAccountRequest
        {
            AccountTypeId = 1,
            AccountCode = parent.AccountCode,
            AccountName = parent.AccountName,
            ParentAccountId = parent.AccountId,
            IsActive = true,
        }, CancellationToken.None);
        Assert.Equal(SaveAccountOutcome.InvalidParent, selfResult.Outcome);

        // Circular parenting: Setting child as parent of its own parent
        SaveAccountResult cycleResult = await h.Accounts.UpdateAsync(parent.AccountId, new SaveAccountRequest
        {
            AccountTypeId = 1,
            AccountCode = parent.AccountCode,
            AccountName = parent.AccountName,
            ParentAccountId = child.AccountId,
            IsActive = true,
        }, CancellationToken.None);
        Assert.Equal(SaveAccountOutcome.InvalidParent, cycleResult.Outcome);
    }

    [SkippableFact]
    public async Task DocumentChoices_excludes_parent_accounts_from_transaction_selection()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        Account parent = await h.CreateAccountAsync("1201", "Inventory Parent", 1);
        Account child = await h.CreateAccountAsync("1202", "Raw Materials", 1, parent.AccountId);

        IReadOnlyList<AccountChoice> choices = await h.Choices.AccountsAsync(ChoiceUsage.Sales, CancellationToken.None);

        Assert.Contains(choices, c => c.AccountId == child.AccountId);
        Assert.DoesNotContain(choices, c => c.AccountId == parent.AccountId);
    }

    [SkippableFact]
    public async Task Journal_rejects_posting_to_parent_accounts()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        Account parent = await h.CreateAccountAsync("1301", "Bank Group", 1);
        Account child = await h.CreateAccountAsync("1302", "Bank Branch 1", 1, parent.AccountId);
        Account expense = await h.CreateAccountAsync("5301", "Office Rent", 5);

        var request = new SaveJournalRequest
        {
            JournalDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Lines =
            [
                new() { AccountId = parent.AccountId, DebitAmount = 500m, CreditAmount = 0m },
                new() { AccountId = expense.AccountId, DebitAmount = 0m, CreditAmount = 500m },
            ],
        };

        SaveJournalResult result = await h.Journals.CreateAsync(request, CancellationToken.None);
        Assert.Equal(SaveJournalOutcome.AccountNotPostable, result.Outcome);
        Assert.Contains("is a parent account", result.Detail ?? string.Empty);
    }

    [SkippableFact]
    public async Task OpeningBalance_rejects_parent_accounts()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        Account parent = await h.CreateAccountAsync("1401", "Fixed Assets Parent", 1);
        Account child = await h.CreateAccountAsync("1402", "Equipment", 1, parent.AccountId);

        var request = new SaveOpeningBalanceRequest
        {
            AsOfDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Lines =
            [
                new()
                {
                    LineType = OpeningBalanceLineType.GlAccount,
                    AccountId = parent.AccountId,
                    DebitAmount = 1000m,
                    CreditAmount = 0m,
                },
            ],
        };

        OpeningBalanceResult result = await h.OpeningBalances.SaveAsync(request, CancellationToken.None);
        Assert.Equal(OpeningBalanceOutcome.AccountUnusable, result.Outcome);
        Assert.Contains("names a parent account", result.Detail ?? string.Empty);
    }

    [SkippableFact]
    public async Task LedgerPosting_rejects_direct_posting_to_parent_accounts()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        Account parent = await h.CreateAccountAsync("1501", "Receivables Group", 1);
        Account child = await h.CreateAccountAsync("1502", "Trade Debtors", 1, parent.AccountId);
        Account sales = await h.CreateAccountAsync("4501", "Product Sales", 4);

        var request = new PostLedgerRequest
        {
            TransactionTypeCode = "INV",
            SourceDocumentId = 42,
            Legs =
            [
                new() { AccountId = parent.AccountId, DebitAmount = 100m, CreditAmount = 0m },
                new() { AccountId = sales.AccountId, DebitAmount = 0m, CreditAmount = 100m },
            ],
        };

        PostLedgerResult result = await h.Postings.PostAsync(request, CancellationToken.None);
        Assert.Equal(PostLedgerOutcome.AccountLocked, result.Outcome);
        Assert.Contains("is a parent account", result.Detail ?? string.Empty);
    }
}
