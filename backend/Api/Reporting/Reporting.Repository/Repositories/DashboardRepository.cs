using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;

namespace Reporting.Repository.Repositories;

/// <summary>
/// Aggregates real-time figures from the database for the Home Dashboard (TK-165).
/// All queries run under the tenant's global query filter and RLS in ReportingDbContext.
/// </summary>
public sealed class DashboardRepository
{
    private const int ControlLedgerType = 3;
    private const int IncomeAccountType = 4;
    private const int ExpenseAccountType = 5;

    private readonly ReportingDbContext _db;
    private readonly TimeProvider _clock;
    private readonly IFinancialYearProvider _financialYear;

    public DashboardRepository(
        ReportingDbContext db,
        TimeProvider clock,
        IFinancialYearProvider financialYear)
    {
        _db = db;
        _clock = clock;
        _financialYear = financialYear;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        // ---------------------------------------------------------------------
        // 1. Sales today
        // ---------------------------------------------------------------------
        var todayInvoices = _db.Invoices
            .Where(i => i.DocumentDate == today && i.Status == DocumentStatus.Posted);

        decimal salesToday = await todayInvoices.SumAsync(i => (decimal?)i.TotalAmountBase, ct) ?? 0m;
        int invoicesCountToday = await todayInvoices.CountAsync(ct);

        List<long> todayInvoiceIds = await todayInvoices.Select(i => i.InvoiceId).ToListAsync(ct);
        int unpaidInvoicesCountToday = 0;
        if (todayInvoiceIds.Count > 0)
        {
            var todayBalances = await (
                from l in _db.Ledger
                where l.LedgerTypeId == ControlLedgerType
                    && todayInvoiceIds.Contains(l.TransactionId)
                    && l.TransactionTypeCode == "INV"
                group l by l.TransactionId into g
                select g.Sum(x => x.DebitAmountBase - x.CreditAmountBase)
            ).ToListAsync(ct);

            unpaidInvoicesCountToday = todayBalances.Count(b => b > 0m);
        }

        // ---------------------------------------------------------------------
        // 2. Receivables & Aging
        // ---------------------------------------------------------------------
        var postedInvoices = _db.Invoices.Where(i => i.Status == DocumentStatus.Posted);

        var invoiceBalances = await (
            from i in postedInvoices
            join l in _db.Ledger
                on new { Code = i.TransactionTypeCode, Id = i.InvoiceId }
                equals new { Code = l.TransactionTypeCode, Id = l.TransactionId }
            where l.LedgerTypeId == ControlLedgerType
            group l by new { i.InvoiceId, i.DueDate, i.TotalAmountBase, i.ContactId } into g
            select new
            {
                g.Key.InvoiceId,
                g.Key.DueDate,
                g.Key.ContactId,
                Balance = g.Sum(x => x.DebitAmountBase - x.CreditAmountBase)
            }
        ).ToListAsync(ct);

        decimal totalReceivables = 0m;
        decimal overdueReceivables = 0m;

        if (invoiceBalances.Count > 0)
        {
            totalReceivables = invoiceBalances.Where(x => x.Balance > 0m).Sum(x => x.Balance);
            overdueReceivables = invoiceBalances
                .Where(x => x.Balance > 0m && x.DueDate.HasValue && x.DueDate.Value < today)
                .Sum(x => x.Balance);
        }
        else if (await postedInvoices.AnyAsync(ct))
        {
            totalReceivables = await postedInvoices.SumAsync(i => i.TotalAmountBase, ct);
            overdueReceivables = await postedInvoices
                .Where(i => i.DueDate != null && i.DueDate.Value < today)
                .SumAsync(i => i.TotalAmountBase, ct);
        }

        // Top 5 outstanding parties
        var outstandingList = new List<DashboardOutstandingPartyDto>();
        var topUnpaidInvoices = invoiceBalances.Where(x => x.Balance > 0m)
            .OrderByDescending(x => x.Balance)
            .Take(5)
            .ToList();

        if (topUnpaidInvoices.Count > 0)
        {
            var contactIds = topUnpaidInvoices.Select(x => x.ContactId).Distinct().ToList();
            var contacts = await _db.Contacts
                .Where(c => contactIds.Contains(c.ContactId))
                .ToDictionaryAsync(c => c.ContactId, c => c.DisplayName, ct);

            foreach (var inv in topUnpaidInvoices)
            {
                DateOnly due = inv.DueDate ?? today;
                int daysDiff = today.DayNumber - due.DayNumber;
                string overdueStr = daysDiff > 0 ? $"{daysDiff} d" : "Current";
                string tone = daysDiff > 60 ? "danger" : daysDiff > 0 ? "warning" : "info";

                string partyName = contacts.TryGetValue(inv.ContactId, out string? name) ? name : $"Contact #{inv.ContactId}";

                outstandingList.Add(new DashboardOutstandingPartyDto
                {
                    Party = partyName,
                    Due = due.ToString("dd MMM"),
                    Amount = inv.Balance,
                    Overdue = overdueStr,
                    Tone = tone
                });
            }
        }

        // ---------------------------------------------------------------------
        // 3. Payables
        // ---------------------------------------------------------------------
        var postedBills = _db.Bills.Where(b => b.Status == DocumentStatus.Posted);

        var billBalances = await (
            from b in postedBills
            join l in _db.Ledger
                on new { Code = b.TransactionTypeCode, Id = b.BillId }
                equals new { Code = l.TransactionTypeCode, Id = l.TransactionId }
            where l.LedgerTypeId == ControlLedgerType
            group l by new { b.BillId, b.DueDate, b.TotalAmount } into g
            select new
            {
                g.Key.BillId,
                g.Key.DueDate,
                Balance = g.Sum(x => x.CreditAmountBase - x.DebitAmountBase)
            }
        ).ToListAsync(ct);

        decimal totalPayables = 0m;
        decimal overduePayables = 0m;
        DateOnly? nextPayableDueDate = null;

        if (billBalances.Count > 0)
        {
            totalPayables = billBalances.Where(x => x.Balance > 0m).Sum(x => x.Balance);
            overduePayables = billBalances
                .Where(x => x.Balance > 0m && x.DueDate < today)
                .Sum(x => x.Balance);

            nextPayableDueDate = billBalances
                .Where(x => x.Balance > 0m && x.DueDate >= today)
                .OrderBy(x => x.DueDate)
                .Select(x => (DateOnly?)x.DueDate)
                .FirstOrDefault();
        }
        else if (await postedBills.AnyAsync(ct))
        {
            totalPayables = await postedBills.SumAsync(b => b.TotalAmount, ct);
            overduePayables = await postedBills
                .Where(b => b.DueDate < today)
                .SumAsync(b => b.TotalAmount, ct);
            nextPayableDueDate = await postedBills
                .Where(b => b.DueDate >= today)
                .OrderBy(b => b.DueDate)
                .Select(b => (DateOnly?)b.DueDate)
                .FirstOrDefaultAsync(ct);
        }

        // ---------------------------------------------------------------------
        // 4. Cash & Bank
        // ---------------------------------------------------------------------
        int bankAccountsCount = await _db.BankAccounts.CountAsync(ct);
        var bankLedgerAccountIds = await _db.BankAccounts
            .Where(b => b.LedgerAccountId != null)
            .Select(b => b.LedgerAccountId!.Value)
            .ToListAsync(ct);

        var cashAndBankAccounts = await _db.Accounts
            .Where(a => a.IsActive && (a.AccountTypeId == 1 && (
                (a.AccountSystemName != null && (a.AccountSystemName.Contains("Bank") || a.AccountSystemName.Contains("Cash")))
                || (a.AccountName.Contains("Bank") || a.AccountName.Contains("Cash"))
                || bankLedgerAccountIds.Contains(a.AccountId))))
            .Select(a => a.AccountId)
            .ToListAsync(ct);

        decimal cashAndBankTotal = 0m;
        if (cashAndBankAccounts.Count > 0)
        {
            cashAndBankTotal = await _db.Ledger
                .Where(l => cashAndBankAccounts.Contains(l.AccountId))
                .SumAsync(l => (decimal?)(l.DebitAmountBase - l.CreditAmountBase), ct) ?? 0m;
        }

        // ---------------------------------------------------------------------
        // 5. Profit & Loss (FYTD)
        // ---------------------------------------------------------------------
        int fyStartMonth = 4;
        try
        {
            fyStartMonth = await _financialYear.GetStartMonthAsync(ct);
        }
        catch
        {
            fyStartMonth = 4;
        }

        DateOnly fyStart = today.Month >= fyStartMonth
            ? new DateOnly(today.Year, fyStartMonth, 1)
            : new DateOnly(today.Year - 1, fyStartMonth, 1);

        string plPeriodLabel = $"{fyStart:MMMM} – {today:MMMM yyyy}";

        decimal revenue = await (
            from l in _db.Ledger
            join a in _db.Accounts on l.AccountId equals a.AccountId
            where a.IsActive && a.AccountTypeId == IncomeAccountType && l.LedgerDate >= fyStart && l.LedgerDate <= today
            select l.CreditAmountBase - l.DebitAmountBase
        ).SumAsync(ct);

        if (revenue == 0m)
        {
            revenue = await _db.Invoices
                .Where(i => i.Status == DocumentStatus.Posted && i.DocumentDate >= fyStart && i.DocumentDate <= today)
                .SumAsync(i => (decimal?)i.TotalAmountBase, ct) ?? 0m;
        }

        decimal cogs = await (
            from l in _db.Ledger
            join a in _db.Accounts on l.AccountId equals a.AccountId
            where a.IsActive && a.AccountTypeId == ExpenseAccountType
                && ((a.AccountSystemName != null && (a.AccountSystemName.Contains("Cost of Goods") || a.AccountSystemName.Contains("COGS")))
                    || a.AccountName.Contains("Cost of Goods") || a.AccountName.Contains("COGS"))
                && l.LedgerDate >= fyStart && l.LedgerDate <= today
            select l.DebitAmountBase - l.CreditAmountBase
        ).SumAsync(ct);

        if (cogs == 0m)
        {
            cogs = await _db.Bills
                .Where(b => b.Status == DocumentStatus.Posted && b.DocumentDate >= fyStart && b.DocumentDate <= today)
                .SumAsync(b => (decimal?)b.TotalAmount, ct) ?? 0m;
        }

        decimal operatingExpenses = await (
            from l in _db.Ledger
            join a in _db.Accounts on l.AccountId equals a.AccountId
            where a.IsActive && a.AccountTypeId == ExpenseAccountType
                && !((a.AccountSystemName != null && (a.AccountSystemName.Contains("Cost of Goods") || a.AccountSystemName.Contains("COGS")))
                    || a.AccountName.Contains("Cost of Goods") || a.AccountName.Contains("COGS"))
                && l.LedgerDate >= fyStart && l.LedgerDate <= today
            select l.DebitAmountBase - l.CreditAmountBase
        ).SumAsync(ct);

        decimal netProfit = revenue - cogs - operatingExpenses;
        decimal marginPercent = revenue > 0m ? Math.Round((netProfit / revenue) * 100m, 1) : 0m;

        // ---------------------------------------------------------------------
        // 6. 6-Month Trend (Sales & Outstanding)
        // ---------------------------------------------------------------------
        var trendPoints = new List<DashboardMonthlyTrendPointDto>();
        for (int i = 5; i >= 0; i--)
        {
            DateTime monthDt = DateTime.UtcNow.AddMonths(-i);
            var mStart = new DateOnly(monthDt.Year, monthDt.Month, 1);
            var mEnd = mStart.AddMonths(1).AddDays(-1);
            string mLabel = mStart.ToString("MMM");

            decimal mSales = await _db.Invoices
                .Where(x => x.Status == DocumentStatus.Posted && x.DocumentDate >= mStart && x.DocumentDate <= mEnd)
                .SumAsync(x => (decimal?)x.TotalAmountBase, ct) ?? 0m;

            trendPoints.Add(new DashboardMonthlyTrendPointDto
            {
                Month = mLabel,
                Sales = mSales,
                Outstanding = totalReceivables
            });
        }
        decimal totalSalesTrend = trendPoints.Sum(p => p.Sales);

        // ---------------------------------------------------------------------
        // 7. Top products by profit
        // ---------------------------------------------------------------------
        var topProductRows = await (
            from d in _db.InvoiceDetails
            join i in _db.Invoices on d.InvoiceId equals i.InvoiceId
            where i.Status == DocumentStatus.Posted && d.LineType == DocumentLineType.Stock
            group d by new { d.ItemId, d.Description } into g
            select new
            {
                ItemName = g.Key.Description ?? ("Item #" + (g.Key.ItemId ?? 0)),
                TotalQty = g.Sum(x => x.Quantity),
                TotalSales = g.Sum(x => x.TaxableAmount),
                TotalCost = g.Sum(x => x.UnitCost * x.Quantity)
            }
        ).OrderByDescending(x => x.TotalSales - x.TotalCost)
        .Take(5)
        .ToListAsync(ct);

        var topProducts = topProductRows.Select(p =>
        {
            decimal profit = p.TotalSales - p.TotalCost;
            decimal margin = p.TotalSales > 0 ? (profit / p.TotalSales) * 100m : 0m;
            return new DashboardTopProductDto
            {
                Item = p.ItemName,
                Sold = $"{p.TotalQty:N0}",
                Profit = profit,
                Margin = $"{margin:F1}%"
            };
        }).ToList();

        // ---------------------------------------------------------------------
        // 8. Statutory Compliance Calendar
        // ---------------------------------------------------------------------
        DateTime prevMonthDt = DateTime.UtcNow.AddMonths(-1);
        string prevMonthPeriod = prevMonthDt.ToString("MMM yyyy");

        var gstr1Due = new DateOnly(today.Year, today.Month, 11);
        var gstr3bDue = new DateOnly(today.Year, today.Month, 20);
        var tdsDue = new DateOnly(today.Year, today.Month, 7);

        var complianceList = new List<DashboardComplianceItemDto>
        {
            BuildComplianceItem("GSTR-1", prevMonthPeriod, gstr1Due, today),
            BuildComplianceItem("GSTR-3B", prevMonthPeriod, gstr3bDue, today),
            BuildComplianceItem("TDS Deposit", prevMonthPeriod, tdsDue, today),
        };

        return new DashboardSummaryDto
        {
            SalesToday = salesToday,
            InvoicesCountToday = invoicesCountToday,
            UnpaidInvoicesCountToday = unpaidInvoicesCountToday,

            TotalReceivables = totalReceivables,
            OverdueReceivables = overdueReceivables,

            TotalPayables = totalPayables,
            OverduePayables = overduePayables,
            NextPayableDueDate = nextPayableDueDate,

            CashAndBankTotal = cashAndBankTotal,
            BankAccountsCount = bankAccountsCount,

            PlPeriodLabel = plPeriodLabel,
            Revenue = revenue,
            CostOfGoodsSold = cogs,
            OperatingExpenses = operatingExpenses,
            NetProfit = netProfit,
            MarginPercent = marginPercent,

            MonthlyTrend = trendPoints,
            TotalSalesTrend = totalSalesTrend,
            TotalOutstandingTrend = totalReceivables,

            TopProducts = topProducts,
            Outstanding = outstandingList,
            UpcomingCompliance = complianceList
        };
    }

    private static DashboardComplianceItemDto BuildComplianceItem(string form, string period, DateOnly due, DateOnly today)
    {
        string status;
        string tone;
        if (today > due)
        {
            status = "Overdue";
            tone = "danger";
        }
        else if (due.DayNumber - today.DayNumber <= 5)
        {
            status = "Due soon";
            tone = "warning";
        }
        else
        {
            status = "Upcoming";
            tone = "info";
        }

        return new DashboardComplianceItemDto
        {
            Form = form,
            Period = period,
            Due = due.ToString("dd MMM yyyy"),
            Status = status,
            Tone = tone
        };
    }
}
