using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Api.Services;

public class ReconciliationService
{
    private readonly AccountingDbContext _db;

    public ReconciliationService(AccountingDbContext db)
    {
        _db = db;
    }

    public async Task<List<ReconciliationMatchView>> GetSuggestedMatchesAsync(long bankStatementId, CancellationToken ct)
    {
        var statementLines = await _db.BankStatementLines
            .Where(b => b.BankStatementId == bankStatementId && !b.IsReconciled)
            .ToListAsync(ct);

        if (statementLines.Count == 0) return new List<ReconciliationMatchView>();

        var allUnreconciledLedger = await _db.JournalLedger
            .Where(l => !l.IsReconciled)
            .ToListAsync(ct);

        var matches = new List<ReconciliationMatchView>();

        foreach (var line in statementLines)
        {
            // Assuming line.Amount is set. If not, use (line.DepositAmount - line.WithdrawalAmount)
            var amount = line.Amount != 0 ? line.Amount : (line.DepositAmount - line.WithdrawalAmount);
            var minDate = line.TransactionDate.AddDays(-3);
            var maxDate = line.TransactionDate.AddDays(3);

            var ledgerCandidates = allUnreconciledLedger
                .Where(l => l.LedgerDate >= minDate && l.LedgerDate <= maxDate)
                .ToList();

            var potentialMatches = new List<ReconciliationLedgerCandidate>();

            foreach (var candidate in ledgerCandidates)
            {
                // A positive bank line (inflow) means the ledger should have a DEBIT to the Bank Account.
                // A negative bank line (outflow) means the ledger should have a CREDIT to the Bank Account.
                // Or if we match against the offsetting leg, it's vice versa.
                // Assuming we match against the bank account leg itself.
                
                var legAmount = candidate.DebitAmount - candidate.CreditAmount; // Positive = debit, Negative = credit
                
                if (Math.Abs(legAmount - amount) < 0.01m)
                {
                    int score = 0;
                    
                    if (candidate.LedgerDate == line.TransactionDate)
                    {
                        score += 50;
                    }

                    // Fuzzy string match logic
                    string bankDesc = line.Description?.ToLowerInvariant() ?? "";
                    string bankRef = line.ReferenceNo?.ToLowerInvariant() ?? "";
                    string ledgerRef = candidate.TransactionDesc?.ToLowerInvariant() ?? "";

                    if (!string.IsNullOrEmpty(ledgerRef) && (bankDesc.Contains(ledgerRef) || ledgerRef.Contains(bankDesc) || bankRef.Contains(ledgerRef)))
                    {
                        score += 50;
                    }

                    potentialMatches.Add(new ReconciliationLedgerCandidate
                    {
                        JournalLedgerId = candidate.LedgerId,
                        LedgerDate = candidate.LedgerDate,
                        TransactionTypeCode = candidate.TransactionTypeCode,
                        TransactionId = candidate.TransactionId,
                        Amount = legAmount,
                        Description = candidate.TransactionDesc,
                        Score = score
                    });
                }
            }

            matches.Add(new ReconciliationMatchView
            {
                BankStatementLineId = line.BankStatementLineId,
                TransactionDate = line.TransactionDate,
                Description = line.Description,
                ReferenceNo = line.ReferenceNo,
                Amount = amount,
                SuggestedMatches = potentialMatches.OrderByDescending(p => p.Score).ToList()
            });
        }

        return matches;
    }

    public async Task ReconcileAsync(long bankStatementLineId, long journalLedgerId, CancellationToken ct)
    {
        var line = await _db.BankStatementLines.FindAsync(new object[] { bankStatementLineId }, ct);
        var ledger = await _db.JournalLedger.FindAsync(new object[] { journalLedgerId }, ct);

        if (line != null && ledger != null)
        {
            line.IsReconciled = true;
            ledger.IsReconciled = true;
            ledger.BankStatementLineId = line.BankStatementLineId;

            await _db.SaveChangesAsync(ct);
        }
    }
}

public class ReconciliationMatchView
{
    public long BankStatementLineId { get; set; }
    public DateOnly TransactionDate { get; set; }
    public string? Description { get; set; }
    public string? ReferenceNo { get; set; }
    public decimal Amount { get; set; }
    public List<ReconciliationLedgerCandidate> SuggestedMatches { get; set; } = new();
}

public class ReconciliationLedgerCandidate
{
    public long JournalLedgerId { get; set; }
    public DateOnly LedgerDate { get; set; }
    public string TransactionTypeCode { get; set; } = null!;
    public long TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public int Score { get; set; }
}
