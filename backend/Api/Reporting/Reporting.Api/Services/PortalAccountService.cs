using Microsoft.EntityFrameworkCore;
using Reporting.Repository;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;

namespace Reporting.Api.Services;

/// <summary>The portal dashboard's figures for one contact (TK-95).</summary>
public sealed class PortalSummary
{
    /// <summary>The receivable balance net of unallocated advances. Negative when the business owes the contact.</summary>
    public decimal Outstanding { get; set; }

    /// <summary>The part of <see cref="Outstanding"/> on invoices past their due date.</summary>
    public decimal Overdue { get; set; }

    /// <summary>Posted invoices less posted credit notes since the financial year began.</summary>
    public decimal TradeValueThisYear { get; set; }

    public decimal TradeValueAllTime { get; set; }

    public DateOnly FinancialYearStart { get; set; }

    /// <summary>The last five posted invoices and credit notes, newest first.</summary>
    public List<PortalRecentDocument> Recent { get; set; } = [];
}

public sealed class PortalRecentDocument
{
    /// <summary>"Invoice" or "CreditNote".</summary>
    public string Kind { get; set; } = string.Empty;

    public long Id { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public decimal TotalAmount { get; set; }
}

/// <summary>One side of a contact's statement: what they owe, or what they are owed.</summary>
public sealed class PortalStatementSide
{
    public decimal OpeningBalance { get; set; }

    public List<PortalStatementLine> Transactions { get; set; } = [];

    public decimal ClosingBalance { get; set; }
}

public sealed class PortalStatementLine
{
    public DateOnly LedgerDate { get; set; }

    /// <summary>The number on the document's face; <c>{code}-{id}</c> only for rows posted before the ledger carried it.</summary>
    public string DocumentNo { get; set; } = string.Empty;

    public string TransactionTypeCode { get; set; } = string.Empty;

    public long TransactionId { get; set; }

    public string? Description { get; set; }

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }

    public decimal Balance { get; set; }
}

/// <summary>
/// A contact's statement: the receivable side always, the payable side when
/// they are also a vendor (TK-95).
/// </summary>
public sealed class PortalStatement
{
    public PortalStatementSide Receivable { get; set; } = new();

    public PortalStatementSide? Payable { get; set; }
}

/// <summary>
/// What the client portal reads about a contact's account (TK-95, design
/// "Client portal"): the dashboard's figures and the statement. Every query is
/// held to the one contact the caller names, which the controller takes from
/// the portal token only, and runs under the branch's query filter and RLS.
/// </summary>
public sealed class PortalAccountService
{
    /// <summary><c>acc.SubAccounts.ReferenceType</c> 1 — a contact.</summary>
    private const int ContactReference = 1;

    private const int AssetType = 1;
    private const int LiabilityType = 2;

    /// <summary><c>mst.LedgerTypes</c> 3 — the control leg a document and its settlements share.</summary>
    private const int ControlLedgerType = 3;

    private readonly IPortalAccountData _db;
    private readonly IFinancialYearProvider _financialYear;
    private readonly TimeProvider _clock;

    public PortalAccountService(IPortalAccountData db, IFinancialYearProvider financialYear, TimeProvider clock)
    {
        _db = db;
        _financialYear = financialYear;
        _clock = clock;
    }

    public async Task<PortalSummary> SummaryAsync(long contactId, CancellationToken ct)
    {
        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        DateOnly yearStart = FinancialYearStart(today, await _financialYear.GetStartMonthAsync(ct));

        List<long> receivable = await SubAccountsAsync(contactId, AssetType, ct);

        decimal outstanding = receivable.Count == 0
            ? 0m
            : await _db.Ledger
                .Where(l => l.SubAccountId != null && receivable.Contains(l.SubAccountId.Value))
                .SumAsync(l => l.DebitAmountBase - l.CreditAmountBase, ct);

        // Each overdue invoice's own balance: its control leg less what has
        // been allocated against it, the same key Accounting settles on.
        var overdueInvoices = _db.Invoices
            .Where(i => i.ContactId == contactId
                && i.Status == DocumentStatus.Posted
                && i.DueDate != null
                && i.DueDate < today);

        List<decimal> overdueBalances = await (
            from i in overdueInvoices
            join l in _db.Ledger
                on new { Code = i.TransactionTypeCode, Id = i.InvoiceId }
                equals new { Code = l.TransactionTypeCode, Id = l.TransactionId }
            where l.LedgerTypeId == ControlLedgerType
            group l by i.InvoiceId into g
            select g.Sum(x => x.DebitAmountBase - x.CreditAmountBase))
            .ToListAsync(ct);

        decimal overdue = overdueBalances.Where(b => b > 0m).Sum();

        var invoices = _db.Invoices.Where(i => i.ContactId == contactId && i.Status == DocumentStatus.Posted);
        var credits = _db.CreditNotes.Where(c => c.ContactId == contactId && c.Status == DocumentStatus.Posted);

        decimal invoicedAll = await invoices.SumAsync(i => i.TotalAmountBase, ct);
        decimal creditedAll = await credits.SumAsync(c => c.TotalAmountBase, ct);
        decimal invoicedYear = await invoices.Where(i => i.DocumentDate >= yearStart)
            .SumAsync(i => i.TotalAmountBase, ct);
        decimal creditedYear = await credits.Where(c => c.DocumentDate >= yearStart)
            .SumAsync(c => c.TotalAmountBase, ct);

        List<PortalRecentDocument> recent = [.. (await invoices
                .OrderByDescending(i => i.DocumentDate).ThenByDescending(i => i.InvoiceId)
                .Take(5)
                .Select(i => new PortalRecentDocument
                {
                    Kind = "Invoice", Id = i.InvoiceId, DocumentNo = i.DocumentNo,
                    DocumentDate = i.DocumentDate, TotalAmount = i.TotalAmount,
                })
                .ToListAsync(ct))
            .Concat(await credits
                .OrderByDescending(c => c.DocumentDate).ThenByDescending(c => c.CreditNoteId)
                .Take(5)
                .Select(c => new PortalRecentDocument
                {
                    Kind = "CreditNote", Id = c.CreditNoteId, DocumentNo = c.DocumentNo,
                    DocumentDate = c.DocumentDate, TotalAmount = c.TotalAmount,
                })
                .ToListAsync(ct))
            .OrderByDescending(d => d.DocumentDate)
            .Take(5)];

        return new PortalSummary
        {
            Outstanding = Money(outstanding),

            // The overdue part of what is owed: never more than the balance net
            // of advances, and nothing when the contact is in credit.
            Overdue = Money(Math.Max(0m, Math.Min(overdue, outstanding))),
            TradeValueThisYear = Money(invoicedYear - creditedYear),
            TradeValueAllTime = Money(invoicedAll - creditedAll),
            FinancialYearStart = yearStart,
            Recent = recent,
        };
    }

    public async Task<PortalStatement> StatementAsync(long contactId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        List<long> receivable = await SubAccountsAsync(contactId, AssetType, ct);
        List<long> payable = await SubAccountsAsync(contactId, LiabilityType, ct);

        PortalStatementSide? payableSide = payable.Count == 0 ? null : await SideAsync(payable, from, to, owedToContact: true, ct);

        return new PortalStatement
        {
            Receivable = await SideAsync(receivable, from, to, owedToContact: false, ct),

            // Shown only when the contact has traded as a vendor too.
            Payable = payableSide is { Transactions.Count: > 0 } or { OpeningBalance: not 0m } ? payableSide : null,
        };
    }

    /// <summary>The first day of the financial year <paramref name="today"/> falls in.</summary>
    public static DateOnly FinancialYearStart(DateOnly today, int startMonth)
    {
        int month = startMonth is >= 1 and <= 12 ? startMonth : 4;
        return today.Month >= month ? new DateOnly(today.Year, month, 1) : new DateOnly(today.Year - 1, month, 1);
    }

    private Task<List<long>> SubAccountsAsync(long contactId, int accountTypeId, CancellationToken ct) =>
        _db.SubAccounts
            .Where(s => s.ReferenceType == ContactReference && s.ReferenceId == contactId && s.AccountTypeId == accountTypeId)
            .Select(s => s.SubAccountId)
            .ToListAsync(ct);

    /// <summary>
    /// One side's running statement. A receivable runs debit less credit (what
    /// the contact owes); a payable runs credit less debit (what they are owed).
    /// </summary>
    private async Task<PortalStatementSide> SideAsync(
        List<long> subAccounts, DateOnly? from, DateOnly? to, bool owedToContact, CancellationToken ct)
    {
        if (subAccounts.Count == 0)
        {
            return new PortalStatementSide();
        }

        var rows = _db.Ledger.Where(l => l.SubAccountId != null && subAccounts.Contains(l.SubAccountId.Value));

        decimal opening = from is DateOnly start
            ? await rows.Where(l => l.LedgerDate < start).SumAsync(l => l.DebitAmountBase - l.CreditAmountBase, ct)
            : 0m;

        var period = rows;
        if (from is DateOnly f)
        {
            period = period.Where(l => l.LedgerDate >= f);
        }

        if (to is DateOnly t)
        {
            period = period.Where(l => l.LedgerDate <= t);
        }

        var lines = await period
            .OrderBy(l => l.LedgerDate).ThenBy(l => l.LedgerId)
            .Select(l => new
            {
                l.LedgerDate,
                l.DocumentNo,
                l.TransactionTypeCode,
                l.TransactionId,
                l.TransactionDesc,
                l.DebitAmountBase,
                l.CreditAmountBase,
            })
            .ToListAsync(ct);

        int sign = owedToContact ? -1 : 1;
        decimal running = sign * opening;
        var side = new PortalStatementSide { OpeningBalance = Money(running) };

        foreach (var line in lines)
        {
            running += sign * (line.DebitAmountBase - line.CreditAmountBase);
            side.Transactions.Add(new PortalStatementLine
            {
                LedgerDate = line.LedgerDate,
                DocumentNo = line.DocumentNo ?? $"{line.TransactionTypeCode}-{line.TransactionId}",
                TransactionTypeCode = line.TransactionTypeCode,
                TransactionId = line.TransactionId,
                Description = line.TransactionDesc,
                Debit = line.DebitAmountBase,
                Credit = line.CreditAmountBase,
                Balance = Money(running),
            });
        }

        side.ClosingBalance = Money(running);
        return side;
    }

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// The four sets the portal reads (TK-95): an interface so the arithmetic can
/// be tested over lists, the way the report sources are.
/// </summary>
public interface IPortalAccountData
{
    IQueryable<JournalLedgerRead> Ledger { get; }

    IQueryable<SubAccountRead> SubAccounts { get; }

    IQueryable<InvoiceRead> Invoices { get; }

    IQueryable<CreditNoteRead> CreditNotes { get; }
}

public sealed class ReportingPortalAccountData(ReportingDbContext db) : IPortalAccountData
{
    public IQueryable<JournalLedgerRead> Ledger => db.Ledger.AsNoTracking();

    public IQueryable<SubAccountRead> SubAccounts => db.SubAccounts.AsNoTracking();

    public IQueryable<InvoiceRead> Invoices => db.Invoices.AsNoTracking();

    public IQueryable<CreditNoteRead> CreditNotes => db.CreditNotes.AsNoTracking();
}
