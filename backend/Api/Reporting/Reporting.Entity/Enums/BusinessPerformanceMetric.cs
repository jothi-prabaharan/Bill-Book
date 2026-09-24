namespace Reporting.Entity.Enums;

/// <summary>
/// The ratios the Business Performance report shows, in the order it shows them
/// (D-15, 2026-09-24: Xero-style KPI ratios over a period). The value is the
/// row's position, so it is also the report's default order.
/// </summary>
public enum BusinessPerformanceMetric
{
    GrossProfitMargin = 1,
    NetProfitMargin = 2,
    ReturnOnInvestment = 3,
    AverageDaysCustomersTakeToPay = 4,
    AverageDaysToPaySuppliers = 5,
    CurrentAssetsToLiabilities = 6,
    TermAssetsToLiabilities = 7,
    TotalCashBalance = 8,
}
