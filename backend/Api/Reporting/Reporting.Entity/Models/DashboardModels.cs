using System;
using System.Collections.Generic;

namespace Reporting.Entity.Models;

/// <summary>
/// Real-time summary figures and metrics for the main Home Dashboard (TK-165).
/// </summary>
public sealed class DashboardSummaryDto
{
    // 1. Sales today
    public decimal SalesToday { get; set; }
    public int InvoicesCountToday { get; set; }
    public int UnpaidInvoicesCountToday { get; set; }

    // 2. Receivables
    public decimal TotalReceivables { get; set; }
    public decimal OverdueReceivables { get; set; }

    // 3. Payables
    public decimal TotalPayables { get; set; }
    public decimal OverduePayables { get; set; }
    public DateOnly? NextPayableDueDate { get; set; }

    // 4. Cash & Bank
    public decimal CashAndBankTotal { get; set; }
    public int BankAccountsCount { get; set; }

    // 5. Profit & Loss (FYTD)
    public string PlPeriodLabel { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal CostOfGoodsSold { get; set; }
    public decimal OperatingExpenses { get; set; }
    public decimal NetProfit { get; set; }
    public decimal MarginPercent { get; set; }

    // 6. 6-Month Trend
    public List<DashboardMonthlyTrendPointDto> MonthlyTrend { get; set; } = [];
    public decimal TotalSalesTrend { get; set; }
    public decimal TotalOutstandingTrend { get; set; }

    // 7. Top Products by Profit
    public List<DashboardTopProductDto> TopProducts { get; set; } = [];

    // 8. Outstanding Aging
    public List<DashboardOutstandingPartyDto> Outstanding { get; set; } = [];

    // 9. Statutory Compliance Calendar
    public List<DashboardComplianceItemDto> UpcomingCompliance { get; set; } = [];
}

public sealed class DashboardMonthlyTrendPointDto
{
    public string Month { get; set; } = string.Empty;
    public decimal Sales { get; set; }
    public decimal Outstanding { get; set; }
}

public sealed class DashboardTopProductDto
{
    public string Item { get; set; } = string.Empty;
    public string Sold { get; set; } = string.Empty;
    public decimal Profit { get; set; }
    public string Margin { get; set; } = string.Empty;
}

public sealed class DashboardOutstandingPartyDto
{
    public string Party { get; set; } = string.Empty;
    public string Due { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Overdue { get; set; } = string.Empty;
    public string Tone { get; set; } = "info";
}

public sealed class DashboardComplianceItemDto
{
    public string Form { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string Due { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Tone { get; set; } = "info";
}
