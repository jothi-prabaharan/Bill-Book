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
        var _service = new ReconciliationService(_dbContext);

        // Arrange
        var statement = new BankStatement
        {
            BankStatementId = 1,
            OrgId = _orgId,
            CustomerId = _customerId,
            BankAccountId = 100
        };
        var line = new BankStatementLine
        {
            BankStatementLineId = 10,
            BankStatementId = 1,
            OrgId = _orgId,
            CustomerId = _customerId,
            Amount = 1500.00m,
            TransactionDate = new DateOnly(2026, 8, 15),
            Description = "Payment for Invoice 123",
            ReferenceNo = "REF-999"
        };
        _dbContext.BankStatements.Add(statement);
        _dbContext.BankStatementLines.Add(line);

        var ledgerRow1 = new JournalLedger
        {
            LedgerId = 1001,
            OrgId = _orgId,
            CustomerId = _customerId,
            LedgerDate = new DateOnly(2026, 8, 14), // Within 3 days
            DebitAmount = 1500.00m,
            CurrencyCode = "INR",
            TransactionDesc = "Payment 123",
            AccountId = 100, // Matches bank statement account
            TransactionTypeCode = "RCM"
        };
        var ledgerRow2 = new JournalLedger
        {
            LedgerId = 1002,
            OrgId = _orgId,
            CustomerId = _customerId,
            LedgerDate = new DateOnly(2026, 8, 10), // Too old
            DebitAmount = 1500.00m,
            CurrencyCode = "INR",
            TransactionDesc = "Payment 124",
            AccountId = 100,
            TransactionTypeCode = "RCM"
        };
        var ledgerRow3 = new JournalLedger
        {
            LedgerId = 1003,
            OrgId = _orgId,
            CustomerId = _customerId,
            LedgerDate = new DateOnly(2026, 8, 16),
            DebitAmount = 2000.00m, // Wrong amount
            CurrencyCode = "INR",
            TransactionDesc = "Payment 125",
            AccountId = 100,
            TransactionTypeCode = "RCM"
        };

        _dbContext.JournalLedger.AddRange(ledgerRow1, ledgerRow2, ledgerRow3);
        await _dbContext.SaveChangesAsync();

        // Act
        var suggestions = await _service.GetSuggestedMatchesAsync(1, CancellationToken.None);

        // Assert
        Assert.Single(suggestions);
        var sugLine = suggestions.First();
        Assert.Equal(10, sugLine.BankStatementLineId);
        
        Assert.Single(sugLine.SuggestedMatches);
        var match = sugLine.SuggestedMatches.First();
        Assert.Equal(1001, match.JournalLedgerId);
        Assert.Equal(1, match.Score); // Since "123" is in both descriptions
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
    }
}
