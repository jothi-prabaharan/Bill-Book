using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Reconciliation Report (TK-202): one bank account at the end of one day,
/// from the balance in the books to the balance the bank statement should
/// show, beside the balance the imported statements do show.
///
/// <b>What counts as on the statement is what Banking matched.</b> A statement
/// line is matched to one posted money document on the same account (spend
/// money, receive money, or a transfer, from either side), or ignored once it
/// has been dealt with some other way, or left unmatched
/// (<c>BankStatementRepository</c>). So, as at the date:
/// <list type="bullet">
///   <item>a posted document on the account up to the date that no statement
///   line up to the date is matched to is outstanding: a payment the bank has
///   not cleared, or a receipt it has not credited;</item>
///   <item>a statement line up to the date that is unmatched, or matched to a
///   document the books did not have by the date, is not yet in the books;</item>
///   <item>an ignored line was dealt with, and stands on neither side.</item>
/// </list>
/// The books plus outstanding payments, less outstanding receipts, plus the
/// statement lines not in the books, is what the statement should say.
///
/// <b>Every figure is in the account's own currency.</b> An amount in that
/// currency counts as it is; one in any other currency counts at its base
/// amount, which is exact for a base-currency account and for a foreign one
/// whose entries are in its own currency, as Banking keeps them. The balance in
/// base currency is given beside it.
/// </summary>
public sealed class BankReconciliationReportRepository
{
    /// <summary>The document type codes a statement line is matched to, as Banking writes them.</summary>
    public const string SpendMoney = "SPM";
    public const string ReceiveMoney = "RCM";
    public const string TransferMoney = "TRM";

    private readonly ReportingDbContext _db;

    public BankReconciliationReportRepository(ReportingDbContext db) => _db = db;

    /// <summary>The branch's bank accounts, active ones first, by name.</summary>
    public async Task<IReadOnlyList<BankReconciliationAccountDto>> AccountsAsync(CancellationToken ct) =>
        await _db.BankAccounts
            .OrderByDescending(b => b.IsActive)
            .ThenBy(b => b.AccountName)
            .Select(b => new BankReconciliationAccountDto
            {
                BankAccountId = b.BankAccountId,
                AccountName = b.AccountName,
                AccountNumber = b.AccountNumber,
                CurrencyCode = b.CurrencyCode,
                IsActive = b.IsActive,
            })
            .ToListAsync(ct);

    /// <summary>
    /// The reconciliation for a request the controller has already validated;
    /// null when the bank account is not the branch's.
    /// </summary>
    public async Task<BankReconciliationReportResultDto?> RunAsync(
        BankReconciliationReportRequest request, CancellationToken ct)
    {
        long bankAccountId = request.BankAccountId!.Value;
        DateOnly asOf = request.AsOfDate!.Value;

        BankAccountRead? bank = await _db.BankAccounts
            .FirstOrDefaultAsync(b => b.BankAccountId == bankAccountId, ct);

        if (bank is null)
        {
            return null;
        }

        BookBalances book = bank.LedgerAccountId is long ledgerAccountId
            ? await BookBalance(_db.Ledger, ledgerAccountId, bank.CurrencyCode, asOf).FirstOrDefaultAsync(ct)
                ?? new BookBalances()
            : new BookBalances();

        List<ReconciliationDocument> documents =
        [
            .. await SpendDocuments(_db.SpendMoney, _db.Contacts, bankAccountId, bank.CurrencyCode, asOf).ToListAsync(ct),
            .. await ReceiveDocuments(_db.ReceiveMoney, _db.Contacts, bankAccountId, bank.CurrencyCode, asOf).ToListAsync(ct),
            .. await TransferDocuments(_db.TransferMoney, _db.BankAccounts, bankAccountId, bank.CurrencyCode, asOf).ToListAsync(ct),
        ];

        List<ReconciliationStatementLine> lines =
            await StatementLines(_db.BankStatementLines, bankAccountId, asOf).ToListAsync(ct);

        List<ReconciliationStatementClosing> closings = await _db.BankStatements
            .Where(s => s.BankAccountId == bankAccountId && s.ToDate <= asOf && s.ClosingBalance != null)
            .Select(s => new ReconciliationStatementClosing
            {
                BankStatementId = s.BankStatementId,
                ToDate = s.ToDate,
                ClosingBalance = s.ClosingBalance!.Value,
            })
            .ToListAsync(ct);

        return Compose(bank, book, documents, lines, closings, asOf);
    }

    /// <summary>
    /// The account's ledger balance at the end of the day, in its currency and
    /// in base. Built from a plain queryable so a test runs it on a list. LINQ only.
    /// </summary>
    public static IQueryable<BookBalances> BookBalance(
        IQueryable<JournalLedgerRead> ledger, long ledgerAccountId, string currencyCode, DateOnly asOf) =>
        ledger
            .Where(l => l.AccountId == ledgerAccountId && l.LedgerDate <= asOf)
            .GroupBy(l => l.AccountId)
            .Select(g => new BookBalances
            {
                Balance = g.Sum(l => l.CurrencyCode == currencyCode
                    ? l.DebitAmount - l.CreditAmount
                    : l.DebitAmountBase - l.CreditAmountBase),
                BalanceBase = g.Sum(l => l.DebitAmountBase - l.CreditAmountBase),
            });

    /// <summary>Posted spend money paid from the account up to the date, named by its payee. LINQ only.</summary>
    public static IQueryable<ReconciliationDocument> SpendDocuments(
        IQueryable<SpendMoneyRead> spend, IQueryable<ContactRead> contacts,
        long bankAccountId, string currencyCode, DateOnly asOf) =>
        from d in spend
        where d.BankAccountId == bankAccountId && d.Status == MoneyDocumentStatus.Posted && d.TransactionDate <= asOf
        join c in contacts on d.ContactId equals c.ContactId into payees
        from c in payees.DefaultIfEmpty()
        select new ReconciliationDocument
        {
            TransactionTypeCode = SpendMoney,
            TransactionId = d.SpendMoneyId,
            TransactionDate = d.TransactionDate,
            TransactionNo = d.TransactionNo,
            Reference = d.ReferenceNo,
            Description = c != null ? c.DisplayName : d.Memo,
            IsPayment = true,
            Amount = d.CurrencyCode == currencyCode ? d.Amount : d.Amount * d.ExchangeRate,
        };

    /// <summary>Posted receive money paid into the account up to the date, named by its payer. LINQ only.</summary>
    public static IQueryable<ReconciliationDocument> ReceiveDocuments(
        IQueryable<ReceiveMoneyRead> receive, IQueryable<ContactRead> contacts,
        long bankAccountId, string currencyCode, DateOnly asOf) =>
        from d in receive
        where d.BankAccountId == bankAccountId && d.Status == MoneyDocumentStatus.Posted && d.TransactionDate <= asOf
        join c in contacts on d.ContactId equals c.ContactId into payers
        from c in payers.DefaultIfEmpty()
        select new ReconciliationDocument
        {
            TransactionTypeCode = ReceiveMoney,
            TransactionId = d.ReceiveMoneyId,
            TransactionDate = d.TransactionDate,
            TransactionNo = d.TransactionNo,
            Reference = d.ReferenceNo,
            Description = c != null ? c.DisplayName : d.Memo,
            IsPayment = false,
            Amount = d.CurrencyCode == currencyCode ? d.Amount : d.Amount * d.ExchangeRate,
        };

    /// <summary>
    /// Posted transfers out of the account (payments) and into it (receipts) up
    /// to the date, named by the account at the other end. LINQ only.
    /// </summary>
    public static IQueryable<ReconciliationDocument> TransferDocuments(
        IQueryable<TransferMoneyRead> transfers, IQueryable<BankAccountRead> banks,
        long bankAccountId, string currencyCode, DateOnly asOf) =>
        from t in transfers
        where (t.FromBankAccountId == bankAccountId || t.ToBankAccountId == bankAccountId)
            && t.Status == MoneyDocumentStatus.Posted && t.TransactionDate <= asOf
        let isOut = t.FromBankAccountId == bankAccountId
        let other = banks
            .Where(b => b.BankAccountId == (isOut ? t.ToBankAccountId : t.FromBankAccountId))
            .Select(b => b.AccountName)
            .FirstOrDefault()
        select new ReconciliationDocument
        {
            TransactionTypeCode = TransferMoney,
            TransactionId = t.TransferMoneyId,
            TransactionDate = t.TransactionDate,
            TransactionNo = t.TransactionNo,
            Reference = t.ReferenceNo,
            Description = (isOut ? "Transfer to " : "Transfer from ") + (other ?? "another account"),
            IsPayment = isOut,
            Amount = t.CurrencyCode == currencyCode ? t.Amount : t.Amount * t.ExchangeRate,
        };

    /// <summary>The account's statement lines up to the date, every status. LINQ only.</summary>
    public static IQueryable<ReconciliationStatementLine> StatementLines(
        IQueryable<BankStatementLineRead> lines, long bankAccountId, DateOnly asOf) =>
        lines
            .Where(l => l.BankAccountId == bankAccountId && l.TransactionDate <= asOf)
            .Select(l => new ReconciliationStatementLine
            {
                BankStatementLineId = l.BankStatementLineId,
                BankStatementId = l.BankStatementId,
                LineNumber = l.LineNumber,
                TransactionDate = l.TransactionDate,
                Description = l.Description,
                ReferenceNo = l.ReferenceNo,
                DepositAmount = l.DepositAmount,
                WithdrawalAmount = l.WithdrawalAmount,
                RunningBalance = l.RunningBalance,
                Status = l.Status,
                MatchedTransactionTypeCode = l.MatchedTransactionTypeCode,
                MatchedTransactionId = l.MatchedTransactionId,
            });

    /// <summary>The reconciliation from what was read. Pure.</summary>
    public static BankReconciliationReportResultDto Compose(
        BankAccountRead bank,
        BookBalances book,
        IReadOnlyList<ReconciliationDocument> documents,
        IReadOnlyList<ReconciliationStatementLine> lines,
        IReadOnlyList<ReconciliationStatementClosing> closings,
        DateOnly asOf)
    {
        List<ReconciliationStatementLine> upToDate = [.. lines.Where(l => l.TransactionDate <= asOf)];
        List<ReconciliationDocument> inBooks = [.. documents.Where(d => d.TransactionDate <= asOf)];

        HashSet<(string, long)> onStatement =
        [
            .. upToDate
                .Where(l => l.Status == StatementLineStatus.Matched
                    && l.MatchedTransactionTypeCode is not null && l.MatchedTransactionId is not null)
                .Select(l => (l.MatchedTransactionTypeCode!.ToUpperInvariant(), l.MatchedTransactionId!.Value)),
        ];
        HashSet<(string, long)> booked = [.. inBooks.Select(d => (d.TransactionTypeCode, d.TransactionId))];

        List<ReconciliationDocument> outstanding =
        [
            .. inBooks
                .Where(d => !onStatement.Contains((d.TransactionTypeCode, d.TransactionId)))
                .OrderBy(d => d.TransactionDate)
                .ThenBy(d => d.TransactionNo, StringComparer.Ordinal)
                .ThenBy(d => d.TransactionId),
        ];

        List<ReconciliationStatementLine> notInBooks =
        [
            .. upToDate
                .Where(l => l.Status == StatementLineStatus.Unmatched
                    || (l.Status == StatementLineStatus.Matched
                        && !(l.MatchedTransactionTypeCode is string code && l.MatchedTransactionId is long id
                            && booked.Contains((code.ToUpperInvariant(), id)))))
                .OrderBy(l => l.TransactionDate)
                .ThenBy(l => l.BankStatementId)
                .ThenBy(l => l.LineNumber),
        ];

        List<BankReconciliationItemDto> payments = [.. outstanding.Where(d => d.IsPayment).Select(ToItem)];
        List<BankReconciliationItemDto> receipts = [.. outstanding.Where(d => !d.IsPayment).Select(ToItem)];
        List<BankReconciliationItemDto> statementItems = [.. notInBooks.Select(ToItem)];

        List<BankReconciliationGroupDto> groups =
        [
            new()
            {
                Group = BankReconciliationGroup.OutstandingPayments,
                Title = TitleOf(BankReconciliationGroup.OutstandingPayments),
                Items = payments,
                Adjustment = payments.Sum(i => i.AmountOut),
            },
            new()
            {
                Group = BankReconciliationGroup.OutstandingReceipts,
                Title = TitleOf(BankReconciliationGroup.OutstandingReceipts),
                Items = receipts,
                Adjustment = -receipts.Sum(i => i.AmountIn),
            },
            new()
            {
                Group = BankReconciliationGroup.StatementLinesNotInBooks,
                Title = TitleOf(BankReconciliationGroup.StatementLinesNotInBooks),
                Items = statementItems,
                Adjustment = statementItems.Sum(i => i.AmountIn - i.AmountOut),
            },
        ];

        decimal expected = book.Balance + groups.Sum(g => g.Adjustment);
        (decimal? statementBalance, DateOnly? statementDate) = StatementBalance(upToDate, closings, asOf);
        decimal? difference = statementBalance - expected;

        return new BankReconciliationReportResultDto
        {
            AsOfDate = asOf,
            BankAccountId = bank.BankAccountId,
            AccountName = bank.AccountName,
            AccountNumber = bank.AccountNumber,
            CurrencyCode = bank.CurrencyCode,
            BookBalance = book.Balance,
            BookBalanceBase = book.BalanceBase,
            Groups = groups,
            ExpectedStatementBalance = expected,
            StatementBalance = statementBalance,
            StatementBalanceDate = statementDate,
            Difference = difference,
            IsReconciled = difference == 0m,
        };
    }

    /// <summary>
    /// What the imported statements say the balance is at the end of the day,
    /// and the last date they state it on. The latest balance the bank gave on
    /// or before the day (a line's running balance, or a statement's closing
    /// balance, whichever is later) is carried forward by the lines after it, so
    /// a statement imported without running balances still reads to the day.
    /// Null when no statement gives a balance by then. Pure.
    /// </summary>
    public static (decimal? Balance, DateOnly? Date) StatementBalance(
        IReadOnlyList<ReconciliationStatementLine> lines,
        IReadOnlyList<ReconciliationStatementClosing> closings,
        DateOnly asOf)
    {
        List<ReconciliationStatementLine> ordered =
        [
            .. lines
                .Where(l => l.TransactionDate <= asOf)
                .OrderBy(l => l.TransactionDate)
                .ThenBy(l => l.BankStatementId)
                .ThenBy(l => l.LineNumber),
        ];

        int anchor = ordered.FindLastIndex(l => l.RunningBalance is not null);
        ReconciliationStatementClosing? closing = closings
            .Where(c => c.ToDate <= asOf)
            .OrderBy(c => c.ToDate)
            .ThenBy(c => c.BankStatementId)
            .LastOrDefault();

        if (anchor >= 0 && (closing is null || ordered[anchor].TransactionDate >= closing.ToDate))
        {
            decimal after = ordered.Skip(anchor + 1).Sum(l => l.DepositAmount - l.WithdrawalAmount);
            return (ordered[anchor].RunningBalance!.Value + after, ordered[anchor].TransactionDate);
        }

        if (closing is not null)
        {
            decimal after = ordered
                .Where(l => l.TransactionDate > closing.ToDate)
                .Sum(l => l.DepositAmount - l.WithdrawalAmount);
            return (closing.ClosingBalance + after, closing.ToDate);
        }

        return (null, null);
    }

    /// <summary>The words a group is shown under. Pure.</summary>
    public static string TitleOf(BankReconciliationGroup group) => group switch
    {
        BankReconciliationGroup.OutstandingPayments => "Payments not yet on the statement",
        BankReconciliationGroup.OutstandingReceipts => "Receipts not yet on the statement",
        _ => "Statement lines not yet in the books",
    };

    private static BankReconciliationItemDto ToItem(ReconciliationDocument d) => new()
    {
        TransactionTypeCode = d.TransactionTypeCode,
        TransactionId = d.TransactionId,
        TransactionDate = d.TransactionDate,
        TransactionNo = d.TransactionNo,
        Reference = d.Reference,
        Description = d.Description,
        AmountIn = d.IsPayment ? 0m : d.Amount,
        AmountOut = d.IsPayment ? d.Amount : 0m,
    };

    private static BankReconciliationItemDto ToItem(ReconciliationStatementLine l) => new()
    {
        BankStatementLineId = l.BankStatementLineId,
        TransactionDate = l.TransactionDate,
        Reference = l.ReferenceNo,
        Description = l.Description,
        AmountIn = l.DepositAmount,
        AmountOut = l.WithdrawalAmount,
    };
}

/// <summary>A bank account's ledger balance, in its currency and in base.</summary>
public sealed class BookBalances
{
    public decimal Balance { get; set; }
    public decimal BalanceBase { get; set; }
}

/// <summary>A posted money document on the account, in the account's currency.</summary>
public sealed class ReconciliationDocument
{
    public string TransactionTypeCode { get; set; } = string.Empty;
    public long TransactionId { get; set; }
    public DateOnly TransactionDate { get; set; }
    public string? TransactionNo { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }

    /// <summary>Money out of the account; otherwise money in.</summary>
    public bool IsPayment { get; set; }

    public decimal Amount { get; set; }
}

/// <summary>A statement line on the account as the reconciliation needs it.</summary>
public sealed class ReconciliationStatementLine
{
    public long BankStatementLineId { get; set; }
    public long BankStatementId { get; set; }
    public int LineNumber { get; set; }
    public DateOnly TransactionDate { get; set; }
    public string? Description { get; set; }
    public string? ReferenceNo { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal WithdrawalAmount { get; set; }
    public decimal? RunningBalance { get; set; }
    public StatementLineStatus Status { get; set; }
    public string? MatchedTransactionTypeCode { get; set; }
    public long? MatchedTransactionId { get; set; }
}

/// <summary>An imported statement's closing balance and the day it closes on.</summary>
public sealed class ReconciliationStatementClosing
{
    public long BankStatementId { get; set; }
    public DateOnly ToDate { get; set; }
    public decimal ClosingBalance { get; set; }
}
