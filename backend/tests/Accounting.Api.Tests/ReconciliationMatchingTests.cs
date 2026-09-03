using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Accounting.Api.Services;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;
using Xunit;
using Shared.Kernel.Tests;

namespace Accounting.Api.Tests;

[Collection(nameof(PostgresCollection))]
public class ReconciliationMatchingTests : IAsyncDisposable
{
    private readonly PostgresFixture _postgres;
    private AccountingDbContext? _dbContext;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    public ReconciliationMatchingTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [SkippableFact]
    public async Task GetSuggestedMatches_FindsExactAmountWithinThreeDays()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        _dbContext = _postgres.CreateContext(_customerId, _orgId);
        var service = new ReconciliationService(_dbContext);

        // Arrange.
        //
        // **Every id here is the one the database assigned.** The first version
        // of this test wrote its own — BankAccountId 100, LedgerId 1001 — and
        // that made it pass exactly once per database: the second run collided
        // on the primary key. It also named a bank account and a ledger account
        // that were never created, and both links are real foreign keys, so the
        // insert was refused before the matcher was ever called. The test was
        // failing on its own fixture rather than on anything the matcher did.
        var ledgerAccount = new Account
        {
            OrgId = _orgId,
            CustomerId = _customerId,
            AccountTypeId = 1,
            AccountCode = "1100",
            AccountName = "Bank",
            IsActive = true,
        };
        _dbContext.Accounts.Add(ledgerAccount);

        // A current account has to name its institution: chk_bank_account_
        // institution exempts only Cash and Wallet, on the grounds that an
        // account with no bank behind it is not one anybody reconciles.
        var bank = new Bank
        {
            OrgId = _orgId,
            CustomerId = _customerId,
            BankCode = "TESTBANK",
            BankName = "Test Bank",
            IsActive = true,
        };
        _dbContext.Banks.Add(bank);
        await _dbContext.SaveChangesAsync();

        var bankAccount = new BankAccount
        {
            OrgId = _orgId,
            CustomerId = _customerId,
            BankId = bank.BankId,
            LedgerAccountId = ledgerAccount.AccountId,
            AccountName = "Current Account",
            AccountNumber = "000111222333",
            CurrencyCode = "INR",
        };
        _dbContext.BankAccounts.Add(bankAccount);
        await _dbContext.SaveChangesAsync();

        var statement = new BankStatement
        {
            OrgId = _orgId,
            CustomerId = _customerId,
            BankAccountId = bankAccount.BankAccountId,
        };
        _dbContext.BankStatements.Add(statement);
        await _dbContext.SaveChangesAsync();

        var line = new BankStatementLine
        {
            BankStatementId = statement.BankStatementId,
            BankAccountId = bankAccount.BankAccountId,
            OrgId = _orgId,
            CustomerId = _customerId,

            // Money in, so it is a deposit. chk_statement_line_exclusive holds
            // the same in-xor-out rule the ledger does: a line that is both, or
            // neither, is one the import failed to understand.
            DepositAmount = 1500.00m,
            WithdrawalAmount = 0m,
            Amount = 1500.00m,
            TransactionDate = new DateOnly(2026, 8, 15),
            Description = "Payment for Invoice 123",
            ReferenceNo = "REF-999",

            // What the importer would have hashed the row to. Required, and it
            // is what makes re-importing an overlapping statement safe.
            RowHash = $"line-{Guid.NewGuid():N}",
        };
        _dbContext.BankStatementLines.Add(line);

        JournalLedger Ledger(DateOnly date, decimal debit, string description) => new()
        {
            OrgId = _orgId,
            CustomerId = _customerId,
            LedgerDate = date,
            DebitAmount = debit,
            CurrencyCode = "INR",
            TransactionDesc = description,
            AccountId = ledgerAccount.AccountId,
            TransactionTypeCode = "RCM",
        };

        // The only one that should be offered: right amount, inside the window.
        var withinWindow = Ledger(new DateOnly(2026, 8, 14), 1500.00m, "Payment 123");

        // Right amount, five days earlier — outside the ±3 day window.
        var tooOld = Ledger(new DateOnly(2026, 8, 10), 1500.00m, "Payment 124");

        // Inside the window, wrong amount.
        var wrongAmount = Ledger(new DateOnly(2026, 8, 16), 2000.00m, "Payment 125");

        _dbContext.JournalLedger.AddRange(withinWindow, tooOld, wrongAmount);
        await _dbContext.SaveChangesAsync();

        // Act
        var suggestions = await service.GetSuggestedMatchesAsync(
            statement.BankStatementId, CancellationToken.None);

        // Assert
        var sugLine = Assert.Single(suggestions);
        Assert.Equal(line.BankStatementLineId, sugLine.BankStatementLineId);

        var match = Assert.Single(sugLine.SuggestedMatches);
        Assert.Equal(withinWindow.LedgerId, match.JournalLedgerId);

        // **The score is out of 100, not out of 1**, and this candidate earns
        // neither half: fifty for landing on the same day as the statement line
        // (it is a day earlier) and fifty for one description containing the
        // other whole ("payment for invoice 123" and "payment 123" share a
        // number, not a substring). Nought is therefore the right answer for a
        // candidate that matches on amount and window alone — which is still
        // worth offering, and is offered last. The original test asserted 1 on
        // a scale that has never existed.
        Assert.Equal(0, match.Score);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
    }
}
