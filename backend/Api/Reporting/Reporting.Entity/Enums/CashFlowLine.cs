namespace Reporting.Entity.Enums;

/// <summary>
/// A line of the direct cash flow statement (TK-201), each belonging to one
/// <see cref="CashFlowActivity"/> except the exchange-rate effect, which sits
/// between the net change and the closing cash.
/// </summary>
public enum CashFlowLine
{
    /// <summary>Accounts receivable and sales accounts: what customers paid, less refunds.</summary>
    ReceiptsFromCustomers = 1,

    /// <summary>Accounts payable, salaries payable, stock and every expense account.</summary>
    PaymentsToSuppliersAndEmployees = 2,

    /// <summary>GST and TDS paid to or refunded by the government, or otherwise standing alone in a posting.</summary>
    GstAndTds = 3,

    /// <summary>Every other income, asset and liability account.</summary>
    OtherOperating = 4,

    /// <summary>Postings that moved cash into fixed assets.</summary>
    FixedAssetPurchases = 5,

    /// <summary>Postings that brought cash in from fixed assets: disposal proceeds.</summary>
    FixedAssetSales = 6,

    /// <summary>Equity accounts other than opening balances.</summary>
    CapitalAndDrawings = 7,

    /// <summary>Revaluing foreign-currency cash and bank accounts: a change in cash that is not a flow.</summary>
    ExchangeRateEffect = 8,
}
